document.addEventListener("DOMContentLoaded", function () {
    const toggle = document.getElementById("sidebarToggle");
    const backdrop = document.getElementById("sidebarBackdrop");
    const dateLabel = document.getElementById("currentDate");

    function closeSidebar() {
        document.body.classList.remove("sidebar-open");
        toggle?.setAttribute("aria-expanded", "false");
    }

    toggle?.addEventListener("click", function () {
        const isOpen = document.body.classList.toggle("sidebar-open");
        toggle.setAttribute("aria-expanded", String(isOpen));
    });

    backdrop?.addEventListener("click", closeSidebar);
    window.addEventListener("resize", function () {
        if (window.innerWidth > 768) closeSidebar();
    });

    if (dateLabel) {
        const parts = new Intl.DateTimeFormat("en-GB", {
            day: "2-digit", month: "short", year: "numeric", weekday: "long"
        }).formatToParts(new Date());
        const value = Object.fromEntries(parts.map(part => [part.type, part.value]));
        dateLabel.textContent = `${value.day} ${value.month} ${value.year}, ${value.weekday}`;
    }

    const tabContainer = document.getElementById("workspaceTabs");
    const dynamicTabs = document.getElementById("dynamicWorkspaceTabs");

    if (!tabContainer || !dynamicTabs) return;

    const userKey = tabContainer.dataset.userKey || "anonymous";
    const storageKey = `aml-workspace-tabs-${userKey}`;
    const dashboardUrl = tabContainer.dataset.dashboardUrl;

    const currentUrl = tabContainer.dataset.currentUrl;
    const currentTitle = tabContainer.dataset.currentTitle || "Page";
    const workspaceKey =
        tabContainer.dataset.workspaceKey || currentUrl;

    const workspaceUrl =
        tabContainer.dataset.workspaceUrl || currentUrl;

    const isDashboard = tabContainer.dataset.isDashboard === "true";

    function getTabs() {
        try {
            const savedTabs = JSON.parse(sessionStorage.getItem(storageKey));
            return Array.isArray(savedTabs) ? savedTabs : [];
        } catch {
            return [];
        }
    }

    function saveTabs(tabs) {
        try { sessionStorage.setItem(storageKey, JSON.stringify(tabs)); } catch { /* Storage may be disabled. */ }
    }

    // A browser refresh starts a fresh workspace. Normal navigation retains tabs.
    const navigation = performance.getEntriesByType("navigation")[0];
    const isReload = navigation?.type === "reload";
    let tabs = isReload ? [] : getTabs();
    if (isReload) {
        saveTabs([]);
        // Dashboard is the only permanent tab after any browser reload.
        if (!isDashboard) {
            window.location.replace(dashboardUrl);
            return;
        }
    }

    // Dashboard is permanent. Every other MVC page becomes a closable tab.
    if (!isDashboard) {
        const existingTab =
            tabs.find(tab =>
                tab.key === workspaceKey);

        if (existingTab) {
            existingTab.title = currentTitle;
            existingTab.currentUrl = currentUrl;
            existingTab.url = workspaceUrl;
        }
        else {
            tabs.push({
                key: workspaceKey,
                title: currentTitle,
                url: workspaceUrl,
                currentUrl: currentUrl
            });
        }
        // Keep the tab strip manageable on smaller screens.
        tabs = tabs.slice(-8);
        saveTabs(tabs);
    }

    function renderTabs() {
        dynamicTabs.replaceChildren();

        tabs.forEach(function (tab) {
            const tabElement = document.createElement("div");
            tabElement.className = "workspace-tab closable-tab";

            if (!isDashboard && tab.key === workspaceKey) {
                tabElement.classList.add("active");
            }

            const link = document.createElement("a");
            link.className = "workspace-tab-link";
            link.href = tab.currentUrl || tab.url;
            link.textContent = tab.title;

            const closeButton = document.createElement("button");
            closeButton.type = "button";
            closeButton.className = "workspace-tab-close";
            closeButton.setAttribute("aria-label", `Close ${tab.title}`);
            closeButton.innerHTML = "&times;";

            closeButton.addEventListener("click", function (event) {
                event.preventDefault();
                event.stopPropagation();

                const closingCurrentTab = !isDashboard && tab.key === workspaceKey;
                tabs = tabs.filter(existingTab => existingTab.key !== tab.key);
                saveTabs(tabs);

                if (closingCurrentTab) {
                    window.location.assign(dashboardUrl);
                    return;
                }

                renderTabs();
            });

            tabElement.append(link, closeButton);
            dynamicTabs.appendChild(tabElement);
        });
    }

    renderTabs();
});


