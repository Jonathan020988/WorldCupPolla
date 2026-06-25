const recentViews = new Map();
let observer;
const minimumVisibleRatio = 0.1;

export function observePredictionViews(dotNetReference) {
    if (observer) {
        observer.disconnect();
    }

    observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (!entry.isIntersecting || entry.intersectionRatio < minimumVisibleRatio) {
                continue;
            }

            registerView(entry.target, dotNetReference);
        }
    }, {
        threshold: [minimumVisibleRatio, 0.25, 0.5, 0.75]
    });

    document
        .querySelectorAll("[data-track-prediction-view='true']")
        .forEach(element => observer.observe(element));
}

function registerView(element, dotNetReference) {
    const pollaId = Number(element.dataset.pollaId);
    const ownerId = Number(element.dataset.ownerId);
    const matchId = Number(element.dataset.matchId);

    if (!pollaId || !ownerId || !matchId) {
        return;
    }

    const key = `${pollaId}:${ownerId}:${matchId}`;
    const now = Date.now();
    const lastView = recentViews.get(key) ?? 0;

    // Evita duplicados causados por renders consecutivos, pero conserva visitas posteriores.
    if (now - lastView < 30000) {
        return;
    }

    recentViews.set(key, now);
    dotNetReference
        .invokeMethodAsync("RegistrarVistaPrediccion", pollaId, ownerId, matchId)
        .catch(() => {});
}

export function disconnectPredictionViews() {
    if (observer) {
        observer.disconnect();
        observer = undefined;
    }
}
