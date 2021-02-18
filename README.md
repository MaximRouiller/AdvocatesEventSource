# Advocates Event Source

This project takes all the commits from the [Microsoft Cloud Advocates](https://github.com/MicrosoftDocs/cloud-developer-advocates) repository and re-run through them to generate Domain specific events.

Those events are when an advocate is "Added", "Modified", "Removed". Other events could potentially be generated but those are the most basic one we need so far.

This allow us to replay a timeline of event since the beginning of the repository while keeping everything up to date.

This initial project does require the presence of a Git repository on disk to work as we don't want to exceed the GitHub API throttle limits (also, it's faster).

## Requirements

* This projects runs with the latest version of .NET (previously Core).
* To run successfully, you will need to define an environment variable with your Azure Storage connection string named `StorageAccountConnectionString`.
* You will need to `git clone` the advocates repository locally and changed the hardcoded `gitPath` set in `Program.cs` for it to run properly.
