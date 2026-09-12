(() => {
    const form = document.getElementById("screening-search-form");
    const results = document.getElementById("screening-results");
    const status = document.getElementById("screening-status");
    if (!form || !results || !status) return;
    const inputs = [...form.querySelectorAll('input[name="Names"]')];
    let timer, activeRequest;
    let requestVersion = 0;

    function scheduleSearch(immediate = false) {
        clearTimeout(timer);
        activeRequest?.abort();
        const version = ++requestVersion;
        results.replaceChildren();
        results.setAttribute("aria-busy", "false");
        if (inputs.every(input => !input.value.trim())) {
            status.textContent = "Type at least one character to start.";
            return;
        }
        status.textContent = "Searching…";
        results.setAttribute("aria-busy", "true");
        timer = setTimeout(() => search(version), immediate ? 0 : 300);
    }

    async function search(version) {
        const controller = new AbortController();
        activeRequest = controller;
        try {
            const response = await fetch(form.action, {
                method: "POST", body: new FormData(form), signal: controller.signal,
                credentials: "same-origin", cache: "no-store"
            });
            if (version !== requestVersion) return;
            if (response.redirected || response.status === 401 || response.status === 403) {
                status.textContent = "Your session expired or access was denied. Refresh and sign in again.";
                return;
            }
            if (response.status === 429) {
                status.textContent = "Too many searches. Wait a moment and try again.";
                return;
            }
            if (!response.ok) throw new Error("Search failed");
            const html = await response.text();
            if (version !== requestVersion) return;
            // Same-origin Razor partial; all record fields must remain HTML-encoded.
            results.innerHTML = html;
            status.textContent = "Results are grouped by name. Select a record to view details. This is not a clearance decision.";
        } catch (error) {
            if (error.name === "AbortError" || version !== requestVersion) return;
            results.replaceChildren();
            status.textContent = "Search failed. No screening conclusion was made. Please try again.";
        } finally {
            if (version === requestVersion) results.setAttribute("aria-busy", "false");
        }
    }
    for (const input of inputs) {
        input.addEventListener("input", event => { if (!event.isComposing) scheduleSearch(); });
        input.addEventListener("compositionend", () => scheduleSearch());
    }
    form.addEventListener("submit", event => { event.preventDefault(); scheduleSearch(true); });
})();

(() => {
    const hero = document.querySelector(".screening-hero");
    const results = document.getElementById("screening-results");
    if (!hero || !results) return;
    let activeCard = null, previousScrollY = 0;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const behavior = () => reducedMotion.matches ? "auto" : "smooth";
    results.addEventListener("toggle", event => {
        const card = event.target;
        if (!(card instanceof HTMLDetailsElement) || !card.classList.contains("screening-card")) return;
        if (card.open) {
            if (!activeCard) previousScrollY = window.scrollY;
            activeCard = card;
            results.querySelectorAll(".screening-card[open]").forEach(other => { if (other !== card) other.open = false; });
            hero.classList.add("screening-hidden");
            requestAnimationFrame(() => { if (activeCard === card && card.open) card.scrollIntoView({ behavior: behavior(), block: "start" }); });
        } else if (activeCard === card) {
            activeCard = null;
            hero.classList.remove("screening-hidden");
            window.scrollTo({ top: previousScrollY, behavior: behavior() });
        }
    }, true);
    new MutationObserver(() => {
        if (activeCard && !results.contains(activeCard)) {
            activeCard = null;
            hero.classList.remove("screening-hidden");
        }
    }).observe(results, { childList: true, subtree: true });
})();
