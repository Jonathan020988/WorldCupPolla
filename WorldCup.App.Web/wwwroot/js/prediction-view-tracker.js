const recentViews = new Map();
let observer;

export function observePredictionViews(dotNetReference) {
    if (observer) {
        observer.disconnect();
    }

    observer = new IntersectionObserver(entries => {
        for (const entry of entries) {
            if (!entry.isIntersecting || entry.intersectionRatio < 0.65) {
                continue;
            }

            const element = entry.target;
            const pollaId = Number(element.dataset.pollaId);
            const ownerId = Number(element.dataset.ownerId);
            const matchId = Number(element.dataset.matchId);

            if (!pollaId || !ownerId || !matchId) {
                continue;
            }

            const key = `${pollaId}:${ownerId}:${matchId}`;
            const now = Date.now();
            const lastView = recentViews.get(key) ?? 0;

            // Evita duplicados causados por renders consecutivos, pero conserva visitas posteriores.
            if (now - lastView < 30000) {
                continue;
            }

            recentViews.set(key, now);
            dotNetReference
                .invokeMethodAsync("RegistrarVistaPrediccion", pollaId, ownerId, matchId)
                .catch(() => {});
        }
    }, {
        threshold: [0.65]
    });

    document
        .querySelectorAll("[data-track-prediction-view='true']")
        .forEach(element => observer.observe(element));
}

export function disconnectPredictionViews() {
    if (observer) {
        observer.disconnect();
        observer = undefined;
    }
}
