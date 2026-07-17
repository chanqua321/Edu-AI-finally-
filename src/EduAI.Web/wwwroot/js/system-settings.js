(function () {
    'use strict';

    const sectionFromUrl = () => {
        const params = new URLSearchParams(window.location.search);
        const querySection = params.get('section');
        if (querySection) return querySection;

        const hash = (window.location.hash || '').replace('#', '');
        if (hash) return hash;

        return document.querySelector('.sys-config')?.dataset.activeSection || 'ai';
    };

    const activateSection = (sectionId) => {
        const navLinks = document.querySelectorAll('[data-sys-section]');
        const panels = document.querySelectorAll('[data-sys-panel]');

        navLinks.forEach((link) => {
            const isActive = link.dataset.sysSection === sectionId;
            link.classList.toggle('active', isActive);
            link.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });

        panels.forEach((panel) => {
            const show = panel.dataset.sysPanel === sectionId;
            panel.classList.toggle('d-none', !show);
            panel.toggleAttribute('hidden', !show);
        });

        if (sectionId) {
            history.replaceState(null, '', `#${sectionId}`);
        }
    };

    const bindNavigation = () => {
        document.querySelectorAll('[data-sys-section]').forEach((link) => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const section = link.dataset.sysSection;
                activateSection(section);

                const offcanvasEl = document.getElementById('sysConfigOffcanvas');
                if (offcanvasEl && window.bootstrap?.Offcanvas) {
                    const instance = bootstrap.Offcanvas.getInstance(offcanvasEl);
                    instance?.hide();
                }
            });
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        bindNavigation();
        activateSection(sectionFromUrl());

        window.addEventListener('hashchange', () => {
            activateSection(sectionFromUrl());
        });
    });
})();
