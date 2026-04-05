# Dev Container

This project includes a dev container configuration for zero-install development.

## Using GitHub Codespaces

Click the green **Code** button on GitHub → **Codespaces** tab → **Create codespace on dev**.

## Using VS Code Dev Containers

1. Install the [Dev Containers extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers)
2. Open this repo in VS Code
3. Click "Reopen in Container" when prompted

## After container starts

The Aspire workload is installed automatically. Run the app with:

```bash
cd my-gpx-activities
dotnet run --project my-gpx-activities.AppHost
```

Ports are forwarded automatically — check the VS Code Ports panel for URLs.
