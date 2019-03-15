# Contribute or Provide Feedback for Azure Relay

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Filing Issues](#filing-issues)
- [Pull Requests](#pull-requests)
    - [General guidelines](#general-guidelines)
    - [Testing guidelines](#testing-guidelines)

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Filing Issues

You can find all of the issues that have been filed in the [Issues](https://github.com/Azure/azure-relay-dotnet/issues) section of the repository.

If you encounter any bugs, please file an issue [here](https://github.com/Azure/azure-relay-dotnet/issues/new) and make sure to fill out the provided template with the requested information.

To suggest a new feature or changes that could be made, file an issue the same way you would for a bug, but remove the provided template and replace it with information about your suggestion.

### Pull Requests

If you are thinking about making a large change to this library, **break up the change into small, logical, testable chunks, and organize your pull requests accordingly**.

You can find all of the pull requests that have been opened in the [Pull Request](https://github.com/Azure/azure-relay-dotnet/pulls) section of the repository.

To open your own pull request, click [here](https://github.com/Azure/azure-relay-dotnet/compare). When creating a pull request, keep the following in mind:
- Make sure you are pointing to the fork and branch that your changes were made in
- The pull request template that is provided **should be filled out**; this is not something that should just be deleted or ignored when the pull request is created
    - Deleting or ignoring this template will elongate the time it takes for your pull request to be reviewed
- Please adhere to the guidelines regarding updating style of existing code specified [here](https://github.com/dotnet/corefx/blob/master/Documentation/project-docs/contributing.md#coding-style-changes), namely:
    - **DO NOT** send PRs for style changes. For example, do not send PRs that are focused on changing usage of Int32 to int.
    - **DO NOT** send PRs for upgrading code to use newer language features, though it's ok to use newer language features as part of new code that's written. For example, it's ok to use expression-bodied members as part of new code you write, but do not send a PR focused on changing existing properties or methods to use the feature.
    - **DO** give priority to the current style of the project or file you're changing.

#### General guidelines

The following guidelines must be followed in **EVERY** pull request that is opened.

- Title of the pull request is clear and informative
- There are a small number of commits that each have an informative message
- A description of the changes the pull request makes is included, and a reference to the bug/issue the pull request fixes is included, if applicable
- All files have the Microsoft copyright header

#### Testing guidelines

The following guidelines must be followed in **EVERY** pull request that is opened.

- Pull request includes test coverage for the included changes
- Tests must use xunit
- Test code should not contain hard coded values for resource names or similar values
- Test should not use App.config files for settings
