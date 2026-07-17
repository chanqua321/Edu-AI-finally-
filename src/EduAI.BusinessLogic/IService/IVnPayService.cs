using System.Collections.Generic;

namespace EduAI.BusinessLogic.IService;

public interface IVnPayService
{
    string CreatePaymentUrl(string txnRef, decimal amount, string orderInfo, string ipAddress, string returnUrl);
    bool ValidateSignature(IReadOnlyDictionary<string, string> vnpayParams, string secureHash);
}
