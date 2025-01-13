class DeveloperPortal {
    constructor() {
        this.searchInput = document.getElementById('searchDocs');
        this.searchResults = document.createElement('div');
        this.searchResults.className = 'search-results';
        this.searchIndex = null;
        
        this.initialize();
    }

    async initialize() {
        // Initialize search functionality
        await this.initializeSearch();
        
        // Initialize code highlighting
        this.initializeCodeBlocks();
        
        // Initialize API explorer
        this.initializeApiExplorer();
        
        // Initialize navigation
        this.initializeNavigation();
    }

    async initializeSearch() {
        try {
            const response = await fetch('/docs/content/search-index.json');
            this.searchIndex = await response.json();
            
            this.searchInput.addEventListener('input', 
                this.debounce(this.handleSearch.bind(this), 300));
                
            document.body.appendChild(this.searchResults);
        } catch (error) {
            console.error('Error initializing search:', error);
        }
    }

    async handleSearch(event) {
        const query = event.target.value.toLowerCase();
        if (query.length < 2) {
            this.searchResults.style.display = 'none';
            return;
        }

        const results = this.searchIndex.filter(item => 
            item.title.toLowerCase().includes(query) || 
            item.content.toLowerCase().includes(query)
        ).slice(0, 5);

        this.displaySearchResults(results);
    }

    displaySearchResults(results) {
        this.searchResults.innerHTML = '';
        
        if (results.length === 0) {
            this.searchResults.style.display = 'none';
            return;
        }

        const ul = document.createElement('ul');
        results.forEach(result => {
            const li = document.createElement('li');
            li.innerHTML = `
                <a href="${result.url}">
                    <h4>${this.highlightText(result.title)}</h4>
                    <p>${this.highlightText(result.excerpt)}</p>
                </a>
            `;
            ul.appendChild(li);
        });

        this.searchResults.appendChild(ul);
        this.searchResults.style.display = 'block';
    }

    initializeCodeBlocks() {
        document.querySelectorAll('pre code').forEach(block => {
            hljs.highlightBlock(block);
            
            // Add copy button
            const button = document.createElement('button');
            button.className = 'copy-button';
            button.textContent = 'Copy';
            button.addEventListener('click', () => this.copyCode(block, button));
            block.parentNode.appendChild(button);
        });
    }

    initializeApiExplorer() {
        document.querySelectorAll('.api-example').forEach(example => {
            const tryItButton = example.querySelector('.try-it-button');
            if (tryItButton) {
                tryItButton.addEventListener('click', 
                    () => this.handleTryItClick(example));
            }
        });
    }

    async handleTryItClick(example) {
        const method = example.dataset.method;
        const url = example.dataset.url;
        const requestBody = example.querySelector('.request-body')?.value;

        try {
            const response = await fetch(url, {
                method,
                headers: {
                    'Content-Type': 'application/json',
                    'Accept': 'application/json'
                },
                body: method !== 'GET' ? requestBody : undefined
            });

            const result = await response.json();
            example.querySelector('.response-body').textContent = 
                JSON.stringify(result, null, 2);
        } catch (error) {
            console.error('API request failed:', error);
        }
    }

    initializeNavigation() {
        const currentPath = window.location.pathname;
        document.querySelectorAll('.nav-link').forEach(link => {
            if (link.getAttribute('href') === currentPath) {
                link.classList.add('active');
            }
        });
    }

    copyCode(block, button) {
        navigator.clipboard.writeText(block.textContent).then(() => {
            button.textContent = 'Copied!';
            setTimeout(() => {
                button.textContent = 'Copy';
            }, 2000);
        });
    }

    highlightText(text) {
        const query = this.searchInput.value.toLowerCase();
        if (!query) return text;

        const regex = new RegExp(`(${query})`, 'gi');
        return text.replace(regex, '<mark>$1</mark>');
    }

    debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }
}

// Initialize the portal when the DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.devPortal = new DeveloperPortal();
}); 