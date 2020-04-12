#### OAuth2 and OpenID Connect

### OAuth2 basics

### OpenId Connect basics

### Setting up Identity Server 4

- dotnet CLI templates exist, that can be installed using `dotnet new -u IdentityServer4.Templates`. You can check if these are installed by running `dotnet new`.
- Install the one we want with `dotnet new is4empty`. This one is without any UI, we'll install that later.
- Make sure the IDP project's name is neutral, because IDP will be used by multiple clients.
- Install UI with `dotnet new is4ui`.
- The template installs an Identity Server with a basic, in-memory setup.
  - Define Ids, Apis and Clients in `Config.cs`, but only as a first-hand solution to get started with development.
  - Developer signing keys.

### Open points

- root folder `tempkey.rsa` - what is that?
