# AzDoCLI
Azure DevOps Command Line to easily check work items.

## Features
- List completed work items assigned to you today.
- Show a list of available commands with descriptions.

## Usage

You can run the CLI from the command line or configure your IDE to use the provided launch profiles.

### Examples

- List completed work items:dotnet run --project src/AzDoCLI.App list-completed  or select the `AzDoCLI.App list-completed` profile in your IDE.

- Show help and list available commands:dotnet run --project src/AzDoCLI.App help  or select the `AzDoCLI.App help` profile in your IDE.

## Implemented Commands

- `list-completed`  
  List completed work items assigned to you today.

- `help`  
  Show help and list available commands.

## Configuration

The CLI requires Azure DevOps configuration, which can be provided via environment variables or `appsettings.json`.

- Environment variables:
  - `AZDO_ORG`: Azure DevOps organization
  - `AZDO_PROJECT`: Azure DevOps project
  - `AZDO_PAT`: Personal Access Token
  - `AZDO_USER_EMAIL`: Your Azure DevOps user email

- Or, create an `appsettings.json` file in the output directory with the following structure:{
  "AzureDevOps": {
    "Organization": "your-org",
    "Project": "your-project",
    "PersonalAccessToken": "your-pat",
      "UserEmail": "your-email"
    }
  }
