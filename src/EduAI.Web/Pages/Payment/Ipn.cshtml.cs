using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EduAI.BusinessLogic.IService;
using EduAI.Model.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EduAI.Web.Pages.Payment;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class IpnModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly IVnPayService _vnpayService;

    public IpnModel(IPaymentService paymentService, IVnPayService vnpayService)
    {
        _paymentService = paymentService;
        _vnpayService = vnpayService;
    }

    public async Task<IActionResult> OnGetAsync() => await HandleAsync();

    public async Task<IActionResult> OnPostAsync() => await HandleAsync();

    private async Task<IActionResult> HandleAsync()
    {
        var vnpayParams = new Dictionary<string, string>();
        foreach (var key in Request.Query.Keys)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                vnpayParams[key] = Request.Query[key].ToString();
        }

        if (Request.HasFormContentType)
        {
            foreach (var key in Request.Form.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpayParams[key] = Request.Form[key].ToString();
            }
        }

        if (!vnpayParams.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
            return Content("RspCode=97&Message=Missing signature");

        var validateParams = vnpayParams
            .Where(p => p.Key != "vnp_SecureHash" && p.Key != "vnp_SecureHashType")
            .ToDictionary(p => p.Key, p => p.Value);

        if (!_vnpayService.ValidateSignature(validateParams, secureHash))
            return Content("RspCode=97&Message=Invalid signature");

        var txnRef = vnpayParams.GetValueOrDefault("vnp_TxnRef");
        var providerTxnId = vnpayParams.GetValueOrDefault("vnp_TransactionNo") ?? "VNPAY";
        var responseCode = vnpayParams.GetValueOrDefault("vnp_ResponseCode");
        if (string.IsNullOrWhiteSpace(txnRef))
            return Content("RspCode=01&Message=Order not found");

        var transaction = await _paymentService.GetTransactionByIdAsync(txnRef);
        if (transaction == null)
            return Content("RspCode=01&Message=Order not found");

        if (!decimal.TryParse(vnpayParams.GetValueOrDefault("vnp_Amount"), out var amountCents)
            || amountCents / 100m != transaction.Amount)
        {
            return Content("RspCode=04&Message=Invalid amount");
        }

        if (transaction.Status == TransactionStatus.Success)
            return Content("RspCode=02&Message=Order already confirmed");

        if (responseCode == "00")
        {
            var ok = await _paymentService.CompleteTransactionAsync(txnRef, providerTxnId, TransactionStatus.Success);
            return Content(ok
                ? "RspCode=00&Message=Confirm Success"
                : "RspCode=99&Message=Confirm Failed");
        }

        await _paymentService.CompleteTransactionAsync(txnRef, providerTxnId, TransactionStatus.Failed);
        return Content("RspCode=00&Message=Confirm Success");
    }
}
