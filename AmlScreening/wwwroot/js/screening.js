(() => {
    const form = document.getElementById("screening-search-form");
    const input = document.getElementById("screening-name");
    const results = document.getElementById("screening-results");
    const status = document.getElementById("screening-status");

    if (!form || !input || !results || !status) {
        return;
    }

    let timer;
    let activeRequest;
    let requestVersion = 0;

    function scheduleSearch(immediate = false) {
        clearTimeout(timer);

        // Cancel the previous request when the typed name changes.
        activeRequest?.abort();

        // Prevent an older response from replacing newer results.
        const version = ++requestVersion;
        const name = input.value.trim();

        results.replaceChildren();
        results.setAttribute("aria-busy", "false");

        if (name.length === 0) {
            status.textContent = "Type at least one character to start.";
            return;
        }

        status.textContent = "Searching…";
        results.setAttribute("aria-busy", "true");

        timer = setTimeout(
            () => search(name, version),
            immediate ? 0 : 300
        );
    }

    async function search(name, version) {
        const controller = new AbortController();
        activeRequest = controller;

        const formData = new FormData(form);
        formData.set("name", name);

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                signal: controller.signal,
                credentials: "same-origin",
                cache: "no-store"
            });

            if (version !== requestVersion) {
                return;
            }

            // Cookie authentication may redirect expired sessions to login.
            if (response.redirected ||
                response.status === 401 ||
                response.status === 403) {
                status.textContent =
                    "Your session expired or access was denied. Refresh and sign in again.";
                return;
            }

            if (!response.ok) {
                throw new Error("Search failed.");
            }

            const html = await response.text();

            if (version !== requestVersion) {
                return;
            }

            // This is our own server-rendered Razor partial.
            // Database fields are HTML-encoded by Razor.
            results.innerHTML = html;

            const count = results.querySelectorAll(
                ".screening-card"
            ).length;

            status.textContent = count > 0
                ? `${count} records displayed. Select a name to view details.`
                : "No matching names found. This is not a clearance decision.";
        } catch (error) {
            if (error.name === "AbortError" ||
                version !== requestVersion) {
                return;
            }

            results.replaceChildren();

            status.textContent =
                "Search is temporarily unavailable. Please try again.";
        } finally {
            if (version === requestVersion) {
                results.setAttribute("aria-busy", "false");
            }
        }
    }

    input.addEventListener("input", event => {
        // Wait for composition to finish for multilingual input.
        if (!event.isComposing) {
            scheduleSearch();
        }
    });

    input.addEventListener("compositionend", () => {
        scheduleSearch();
    });

    form.addEventListener("submit", event => {
        event.preventDefault();
        scheduleSearch(true);
    });
})();
(() => {
    const hero = document.querySelector(".screening-hero");
    const results = document.getElementById("screening-results");

    if (!hero || !results) {
        return;
    }

    let activeCard = null;
    let previousScrollY = 0;

    const reducedMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    );

    function restoreSearch(restoreScroll) {
        hero.classList.remove("screening-hidden");
        activeCard = null;

        if (restoreScroll) {
            window.scrollTo({
                top: previousScrollY,
                behavior: reducedMotion.matches ? "auto" : "smooth"
            });
        }
    }

    // Capture is needed because the details "toggle" event
    // does not bubble normally.
    results.addEventListener("toggle", event => {
        const card = event.target;

        if (!(card instanceof HTMLDetailsElement) ||
            !card.classList.contains("screening-card")) {
            return;
        }

        if (card.open) {
            // Save the original position only when entering detail mode.
            if (!activeCard) {
                previousScrollY = window.scrollY;
            }

            activeCard = card;

            // Keep only the selected record expanded.
            results.querySelectorAll(".screening-card[open]")
                .forEach(otherCard => {
                    if (otherCard !== card) {
                        otherCard.open = false;
                    }
                });

            hero.classList.add("screening-hidden");

            requestAnimationFrame(() => {
                if (activeCard === card && card.open) {
                    card.scrollIntoView({
                        behavior: reducedMotion.matches
                            ? "auto"
                            : "smooth",
                        block: "start"
                    });
                }
            });
        } else if (activeCard === card) {
            restoreSearch(true);
        }
    }, true);

    // If live search replaces the results, restore the search area.
    const observer = new MutationObserver(() => {
        if (activeCard && !results.contains(activeCard)) {
            restoreSearch(false);
        }
    });

    observer.observe(results, {
        childList: true,
        subtree: true
    });
})();