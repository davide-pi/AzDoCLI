# AzDoCLI
Azure DevOps Command Line to easily check work items.

## Features
- List completed work items assigned to you for a period (day, week, month).
- List active work items assigned to you (default: today).
- List all (completed and active) work items assigned to you for a period (day, week, month).
- Show a list of available commands with descriptions.

## Usage

You can run the CLI from the command line or configure your IDE to use the provided launch profiles.

### Examples

- List completed work items for today:
  dotnet run --project src/AzDoCLI.App list-completed
- List completed work items for a week:
  dotnet run --project src/AzDoCLI.App list-completed --period week
- List completed work items for a month:
  dotnet run --project src/AzDoCLI.App list-completed --period month
- List active work items for today:
  dotnet run --project src/AzDoCLI.App list-active
- List all (completed and active) work items for today:
  dotnet run --project src/AzDoCLI.App list-all
- List all (completed and active) work items for a week:
  dotnet run --project src/AzDoCLI.App list-all --period week
- Show help and list available commands:
  dotnet run --project src/AzDoCLI.App help

## Implemented Commands

- `list-completed`  
  List completed work items assigned to you for a period. Use `--period`/`-p` to specify:
  - `day`   = today (default)
  - `week`  = this week
  - `month` = this month

- `list-active`  
  List active work items assigned to you (default: today).

- `list-all`  
  List all (completed and active) work items assigned to you for a period. Use `--period`/`-p` to specify:
  - `day`   = today (default)
  - `week`  = this week
  - `month` = this month

- `help`  
  Show help and list available commands.

## Configuration

The CLI requires Azure DevOps configuration, which can be provided via environment variables.

- Environment variables:
  - `AZDO_ORG`: Azure DevOps organization
  - `AZDO_PROJECT`: Azure DevOps project
  - `AZDO_PAT`: Personal Access Token
  - `AZDO_USER_EMAIL`: Your Azure DevOps user email
