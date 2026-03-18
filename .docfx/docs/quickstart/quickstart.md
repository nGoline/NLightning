# Running the Project

This section will guide you through getting a copy of NLightning up and running on your local machine for development
and testing purposes.

## Prerequisites

Before you begin, ensure you have the following installed on your system:

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- Git (for cloning the repository)

## Installation

1 - **Clone the repository**

First, clone the NLightning repository to your local machine using Git:

```sh
git clone https://github.com/ngoline/nlightning.git
cd nlightning
```

2 - **Build the project**

Navigate to the project directory and build the project using the .NET CLI to ensure all dependencies are properly
installed:

```sh
dotnet build
```

3 - **Versioning Policy**

Check the versioning policy of the project [here](versioning)

4 - **Contribute to Development**

As NLightning is currently under active development, it may not be in a runnable state just yet. However, this opens up
a great opportunity for you to contribute. Whether it's implementing new features, fixing bugs, or improving
documentation, your contributions are invaluable to making NLightning fully operational.

To start contributing:

- Explore the [Issues](https://github.com/ngoline/nlightning/issues) section on GitHub to find out what needs to be
  worked on.
- Review our [Contributing Guidelines](contributing) for details on making contributions, such as how to create pull
  requests.
- If you have a new idea or feature you'd like to work on, don't hesitate to open a new issue to discuss it with the
- project maintainers.

We encourage you to dive into the codebase, familiarize yourself with the project structure, and see where your skills
and interests can help drive NLightning forward.

## Testing

To verify that everything is set up correctly, you can run the included unit tests:

```sh
dotnet test
```