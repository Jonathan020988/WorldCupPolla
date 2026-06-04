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

window.worldCupDownloadFile = function (fileName, contentType, base64Data) {
    const link = document.createElement("a");
    link.download = fileName || "archivo.pdf";
    link.href = `data:${contentType || "application/octet-stream"};base64,${base64Data}`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
