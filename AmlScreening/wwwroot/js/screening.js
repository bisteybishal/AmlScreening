// Single-name live search and multiple-name button search.
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
        activeRequest?.abort();

        // An old response must not replace a newer search.
        const version = ++requestVersion;
        const name = input.value.trim();
        const isMultiple = name.includes(",");

        results.replaceChildren();
        results.setAttribute("aria-busy", "false");

        if (!name) {
            status.textContent = "Type a name to start.";
            return;
        }

        if (name.length > 5000) {
            status.textContent =
                "Enter no more than 5,000 characters.";
            return;
        }

        if (!isMultiple && name.length > 200) {
            status.textContent =
                "Enter no more than 200 characters for one name.";
            return;
        }

        // Wait for a button click when commas are present.
        if (isMultiple && !immediate) {
            status.textContent =
                "Multiple names entered. Click Search to check them.";
            return;
        }

        const url = isMultiple
            ? form.dataset.multipleUrl
            : form.action;

        if (!url) {
            status.textContent =
                "The search address is not configured.";
            return;
        }

        status.textContent = isMultiple
            ? "Searching names…"
            : "Searching…";

        results.setAttribute("aria-busy", "true");

        // Single-name live search waits until typing pauses.
        timer = setTimeout(
            () => search(name, url, version),
            immediate ? 0 : 300
        );
    }

    async function search(name, url, version) {
        const controller = new AbortController();
        activeRequest = controller;

        const formData = new FormData(form);
        formData.set("name", name);

        try {
            const response = await fetch(url, {
                method: "POST",
                body: formData,
                signal: controller.signal,
                credentials: "same-origin",
                cache: "no-store"
            });

            if (version !== requestVersion) {
                return;
            }

            if (
                response.redirected ||
                response.status === 401 ||
                response.status === 403
            ) {
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

            // Our Razor partial HTML-encodes displayed data.
            results.innerHTML = html;

            if (results.querySelector(".alert-danger")) {
                status.textContent =
                    "Search could not be completed. See the message below.";
                return;
            }

            const count = results.querySelectorAll(
                ".screening-card"
            ).length;

            status.textContent = count > 0
                ? `${count} result cards displayed. Select a record to view details.`
                : "No matching names found. This is not a clearance decision.";
        } catch (error) {
            if (
                error.name === "AbortError" ||
                version !== requestVersion
            ) {
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

// Open a record's details and restore the search when closed.
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

    results.addEventListener("toggle", event => {
        const card = event.target;

        if (
            !(card instanceof HTMLDetailsElement) ||
            !card.classList.contains("screening-card")
        ) {
            return;
        }

        if (card.open) {
            if (!activeCard) {
                previousScrollY = window.scrollY;
            }

            activeCard = card;

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