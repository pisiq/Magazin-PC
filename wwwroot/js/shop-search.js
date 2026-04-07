(() => {
    const input = document.getElementById("searchQuery");
    const list = document.getElementById("autocompleteResults");
    if (!input || !list) return;

    let debounceId = null;

    const hideList = () => {
        list.innerHTML = "";
        list.classList.add("d-none");
    };

    const renderSuggestions = (items) => {
        if (!items || items.length === 0) {
            hideList();
            return;
        }

        list.innerHTML = items
            .map((item) => {
                const sub = item.subcategoryName ? ` / ${item.subcategoryName}` : "";
                return `
<a class="list-group-item list-group-item-action" href="/Shop/Product/${item.id}">
    <div class="fw-semibold">${item.name}</div>
    <small class="text-muted">${item.categoryName}${sub}</small>
</a>`;
            })
            .join("");

        list.classList.remove("d-none");
    };

    const loadSuggestions = async () => {
        const query = input.value.trim();
        if (query.length < 2) {
            hideList();
            return;
        }

        const categoryElement = document.querySelector("select[name='categoryId']");
        const categoryId = categoryElement && categoryElement.value ? `&categoryId=${encodeURIComponent(categoryElement.value)}` : "";

        try {
            const response = await fetch(`/api/products/autocomplete?q=${encodeURIComponent(query)}&top=8${categoryId}`);
            if (!response.ok) {
                hideList();
                return;
            }

            const suggestions = await response.json();
            renderSuggestions(suggestions);
        } catch {
            hideList();
        }
    };

    input.addEventListener("input", () => {
        clearTimeout(debounceId);
        debounceId = setTimeout(loadSuggestions, 180);
    });

    document.addEventListener("click", (event) => {
        if (event.target !== input && !list.contains(event.target)) {
            hideList();
        }
    });
})();

