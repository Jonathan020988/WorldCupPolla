window.worldCupAdmin = window.worldCupAdmin || {};

window.worldCupAdmin.scrollToSection = function (sectionId) {
    const element = document.getElementById(sectionId);
    if (!element) {
        return;
    }

    element.scrollIntoView({ behavior: "smooth", block: "start" });

    if (window.history && window.history.replaceState) {
        window.history.replaceState(null, "", `${window.location.pathname}#${sectionId}`);
    }
};
