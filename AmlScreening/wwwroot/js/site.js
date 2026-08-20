document.addEventListener("DOMContentLoaded", function () {

    const sidebar = document.getElementById("sidebar");
    const toggle = document.getElementById("sidebarToggle");

    if (!sidebar || !toggle) {
        return;
    }

    toggle.addEventListener("click", function () {

        if (window.innerWidth <= 991) {

            sidebar.classList.toggle("mobile-open");

        } else {

            document.body.classList.toggle("sidebar-collapsed");

        }

    });

});