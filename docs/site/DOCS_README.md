# Configuration.Writable Documentation Site

This is the official documentation site for Configuration.Writable, built with [Docusaurus](https://docusaurus.io/).

## Local Development

### Prerequisites

- Node.js 18+ 
- npm

### Installation

```bash
npm install
```

### Development Server

```bash
npm start
```

This command starts a local development server and opens up a browser window. Most changes are reflected live without having to restart the server.

The site will be available at: `http://localhost:3000/Configuration.Writable/`

### Build

```bash
npm run build
```

This command generates static content into the `build` directory and can be served using any static contents hosting service.

### Serve Built Site

```bash
npm run serve
```

This serves the built site locally for testing.

## Deployment to GitHub Pages

### Option 1: Manual Deployment

```bash
# Build the site
npm run build

# Deploy to GitHub Pages
GIT_USER=<Your GitHub Username> npm run deploy
```

### Option 2: GitHub Actions

Create `.github/workflows/deploy-docs.yml`:

```yaml
name: Deploy Documentation

on:
  push:
    branches:
      - main
    paths:
      - 'docs/site/**'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup Node
        uses: actions/setup-node@v4
        with:
          node-version: 20
          cache: 'npm'
          cache-dependency-path: docs/site/package-lock.json
      
      - name: Install dependencies
        run: npm ci
        working-directory: docs/site
      
      - name: Build
        run: npm run build
        working-directory: docs/site
      
      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs/site/build
      
      - name: Deploy to GitHub Pages
        uses: actions/deploy-pages@v4
```

Then enable GitHub Pages in repository settings:
1. Go to Settings > Pages
2. Source: GitHub Actions

## Documentation Structure

```
docs/
├── intro.md                    # Introduction and quick start
├── getting-started/
│   ├── installation.md         # Installation guide
│   └── setup.md               # Setup guide
├── usage/
│   ├── simple-app.md          # Usage without DI
│   ├── host-app.md            # Usage with DI
│   └── aspnet-core.md         # ASP.NET Core integration
├── customization/
│   ├── configuration.md       # Configuration methods
│   ├── save-location.md       # Save location options
│   ├── format-provider.md     # Format providers
│   ├── file-provider.md       # File provider
│   ├── change-detection.md    # Change detection
│   ├── validation.md          # Validation
│   ├── logging.md             # Logging
│   └── section-name.md        # Section names
├── advanced/
│   ├── native-aot.md          # NativeAOT support
│   └── testing.md             # Testing guide
└── api/
    └── interfaces.md          # API reference
```

## Features

- **Syntax Highlighting**: Powered by Prism with support for C#, JSON, YAML, and Bash
- **Dark Mode**: Automatic theme switching based on system preferences
- **Search**: Built-in search functionality
- **Mobile Responsive**: Optimized for all screen sizes
- **Fast**: Static site generation for excellent performance

## Configuration

The site configuration is in `docusaurus.config.ts`. Key settings:

- `title`: Site title
- `tagline`: Site tagline
- `url`: Production URL
- `baseUrl`: Base URL path (currently `/Configuration.Writable/`)
- `organizationName`: GitHub organization/user
- `projectName`: GitHub repository name

## Adding Content

### Adding a New Page

1. Create a new `.md` file in the appropriate directory under `docs/`
2. Add frontmatter with `sidebar_position`:

```markdown
---
sidebar_position: 1
---

# Page Title

Content here...
```

3. Update `sidebars.ts` if needed to include the new page in the sidebar

### Updating Navigation

Edit `sidebars.ts` to modify the documentation sidebar structure.

## Markdown Features

Docusaurus supports enhanced Markdown features:

- **Code blocks** with syntax highlighting
- **Admonitions** (:::note, :::tip, :::warning, :::danger)
- **Tabs** for multiple code examples
- **MDX** for using React components in Markdown

See [Docusaurus Markdown Features](https://docusaurus.io/docs/markdown-features) for more details.

## License

Same as the main Configuration.Writable project - Apache-2.0
