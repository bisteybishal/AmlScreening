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
});
