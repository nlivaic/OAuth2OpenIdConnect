This is a test.

## OAuth2 and OpenID Connect

### Keeping up to date

- Consult [IETF](https://tools.ietf.org/html/draft-ietf-oauth-security-topics-14) for security best practices regarding OAuth2.

### Basics

#### Flows/grants

- Public client cannot be trusted with client credentials. E.g. browser-side app.
- Private client can be trusted with client credentials. E.g. server-side app.
- Flow depends on the type of client, dependending how the application can achieve authentication.
- Authorization code
- Implicit
- Hybrid

#### Tokens

- Identity token is obtained through authentication via the authorization endpoint at IDP.
- Identity token is used to create a claims identity (`ClaimsPrincipal User`).
- Access token is obtained for authorization via the authorization endpoint at IDP.
- JWT structure:
  - [Specification](https://tools.ietf.org/html/rfc7519)

#### Relevant endpoints

##### Client

- `/signin-oidc`
- `/signout-callback-oidc`

##### IDP

- `.well-known/openid-configuration`
- `/connect/authorize` - authorization endpoint
- `/connect/token` - token endpoint
- `/connect/endsession` - deletes the authentication token on IDP
- `/connect/userinfo` - identity claims for the logged in user

### Setting up Identity Server 4

#### What comes with templates?

- dotnet CLI templates exist, that can be installed using `dotnet new -u IdentityServer4.Templates`. You can check if these are installed by running `dotnet new`.
- Install the one we want with `dotnet new is4empty`. This one is without any UI, we'll install that later.
- Make sure the IDP project's name is neutral, because IDP will be used by multiple clients.
- Install UI with `dotnet new is4ui`.
- Https is already enabled by the `.UseIdentityServer()` middleware.
- We can go to `.well-known/openid-configuration` to get basic data on endpoints.
- Add UI by installing template: `dotnet new is4ui`:
  - To get the UI working you have to uncomment **all** the commented out lines in `Startup.ConfigureServices` and `Startup.Configure`.

#### Setup

- The template installs an Identity Server with a basic, in-memory setup.
  - Define Identity resources, Api resources and Client resources in `Config.cs`, but only as a first-hand solution to get started with development.
  - Define `TestUsers`, but only once you've added the UI.
  - Developer signing keys.
  - `UseIdentityServer()` to set up middleware.
- Test users are added in `TestUsers.cs`:
  - Remove these and add your own.
  - Add your own claims if you want. Make sure you add a `SubjectId` to each test user.
  - Once you are done, add the test users to the identity server by calling `.AddTestUsers(TestUsers.Users)` in `Startup.ConfigureServices()`.
- Identity resources in `Config.cs`:
  - Identity resources are mapped to scopes that give access to identity-related information.
  - `IdentityResources.OpenId()` is mandatory because we are using OIDC. This scope gives us access to user identifier (`subject_id` claim).
  - `IdentityResources.Profile()` gives access to profile-related claims, e.g. `given_name`, `family_name`.
- API resources in `Config.cs`:
  - API resources are mapped to scopes that give access to APIs.
- After you have configured any new identity scopes and claims, you can go to `.well-known/openid-configuration` and you should see both there, under `scopes-supported` and `claims-supported`.
- Client resources:
  - Each client should have defined:
    - Client Name: name to show on login screen.
    - Client Id: unique identifier defining the client.
    - Allowed grants: which flows are supported by this client.
    - Redirect URI: default value, as per standard, is `<domain>/signin-oidc`. You must provide the redirect URI, but a non-default value can be used as well.
    - Allowed scopes: which scopes are allowed by this client.
    - Client secrets

### Setting up client

- The main point here is to set up middleware and services so authentication and authorization is provided. The steps are outlined below, but for details, check [Client project's Startup.cs](src\ImageGallery.Client\Startup.cs). For more info on authentication schemes, consult [here](https://stackoverflow.com/questions/52492666/what-is-the-point-of-configuring-defaultscheme-and-defaultchallengescheme-on-asp/52493428#52493428).
- Configure authentication services by calling `.AddAuthentication()` and providing default and challenge authentication schemas.
- Register and configure Open Id handler with `.AddOpenIdConnect()` to assist in several details. OIDC handler will:
  - Generate authorization token request.
  - Generate token request.
  - Handle identity token validation
- Add an encrypted authentication cookie with `.AddCookie()`.
- By default the OpenIdConnect middleware is defined with the sign in callback endpoint at `/signin-oidc`. This can be overridden by providing a value to the `CallbackPath`.
- Use the above authentication and authorization services as part of authentication and authorization middleware by calling `app.UseAuthentication()` and `app.UseAuthorization()`.

### Authorization code flow

#### Outline

- Utilizes front-channel and back-channel communication.
- Authorization code is a short-lived proof of who the user is. It binds the front-end session between the user and the client with the back-end session between the client server and IDP. It is obtanied from the authorization endpoint.
- Identity token is a JWT token with a list of claims, obtained from the token endpoint. These claims are used by the server to create a claims identity and log the user in using an encrypted cookie.

#### Flow

- User tries to access a resource that is available only to authenticated users.
- Client middleware detects the user is not logged in and cannot authenticate.
- Client middleware redirects the user to the IDP, based on information provided to the middleware.
- User gives credentials to IDP.
- Depending on the `response_mode` the client initially provided to the IDP, the IDP returns to the client an authorization code:
  - `uri` - IDP returns a `302` and the authorization code is embedded in the redirect URI found in the `Location` header. Browser redirects to the default `signin-oidc` (or some other explicitly provided non-default endpoint) endpoint in the client app.
  - `form_post` - IDP returns a `200` and the response payload is a form post. The authorization code is contained within the `<form>` elements as a postback value. The client app's callback `signin-oidc` (or whatever) is in the form's `action` attribute.
- Browser redirects/posts to provided URI.
- On the server:
  - Client middleware intercept the request, finds the authorization code.
  - Sends an identity token request (along with code verifier if PKCE is used) to the **token endpoint**. IDP validates (if PKCE is used) the code verifier and the authorization code, then returns identity token.
  - Identity token and access token are validated on the server.
  - If fetching user claims from the `/userinfo` endpoint, client app passes the access token to the endpoint. Endpoint validates the token and returns the claims associated with the profiles in the access token. Please note there is more on this in the [Identity Claims](#identity-claims) section.
  - Client app creates a claims identity (user). This is what is accessible through `Controller.User`.
  - Authentication ticket is created, encrypted and stored in an encrypted cookie.
- Above steps are facilitated through the use of the Authentication and OpenIdConnect middlewares, but could also be done manually.

#### Checking the token for yourself.

- Since the identity token is obtained through back-channel communication, you cannot see it in Chrome or any of the logs. You can get the token from your controller by calling `HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken)` and writing it out to the debug output.
- Now that you have the token, you can make sure the claims identity user has been created from the token at hand by comparing the token claim `sub` and claim `nameidentifier` from the `User` object. Both should be the same.

#### Authorization Code Injection Attack

- If the attacker gets a hold of the users authorization code, he can swap the user's browser session with his own. This way the attacker now has the victim's privileges. Details can be found [here](https://tools.ietf.org/html/draft-ietf-oauth-security-topics-14#page-21).
- Advised approach with authorization code flow is to utilize PKCE (Proof Key for Code Exchange) alongside it. ![PKCE](https://user-images.githubusercontent.com/26722936/79879157-a0b6a180-83ee-11ea-9523-1e1ee3a9f771.png)
- Setup:
  - At the IDP, you enable it per client, by adding a `RequirePkce = true`.
  - At the client app, configure the `OpenIdConnect` middleware with `options.UsePkce = true` (or just omit the line, since the middleware defaults to true).

#### Logout

- Sign out of the client app by deleting the authentication cookie. This is done by calling `HttpContext.SignOutAsync(CookieAuthenticationScheme.CookieAuthenticationDefaults.LogScheme)`.
- It is not enough to simply log out of the client app, as we will simply keep getting redirected back to the IDP. Since we are still logged in the IDP, it will redirect us back to the client app with a new token. We must also log out of the IDP because that is the only way IDP's authentication cookie can get deleted. This is done by calling `HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme)`. Calling the `SignOutAsync` will redirect to the end session endpoint at the IDP (`/connect/endsession`) causing the IDP's authentication cookie to get deleted. Also note the token itself was sent to the IDP (in the query string, `id_token_hint`) so the IDP can double check who is doing the logout and prevent any attacks.
- Once both of the above are set up, we are logged out of both the client app and IDP. However, two issues exist here. First, if you take a look at the IDP output, you will notice a warning saying "_Invalid PostLogoutRedirectUri_". Second, we are then stuck at the IDP's logged out page, which is not user-friendly. We should tell our app and the IDP where to redirect after the sign out. Both of these points are addressed below:
  - Client app: by default the OpenIdConnect middleware defines the sign out callback endpoint at `/signout-callback-oidc`. This can be overridden by providing a value to the `SignedOutCallbackPath`. In any case, sign out callback URI is sent to the IDP on logout redirect as `post_logout_redirect_uri` query string.
  - IDP: same value **must** be defined on the IDP side as well, per client. Callback URI is provided to the `PostLogoutRedirectUris`, you cannot rely on default URIs here. Because we didn't provide this value earlier, the above "_Invalid PostLogoutRedirectUri_" warning was issued by the IDP.
  - IDP: at this point the IDP does not issue any warnings since callback URIs are sorted out. However, it still does not redirect to the callback URI. This must be manually enabled by setting `AccountOptions.AutomaticRedirectAfterSignOut = true`.

#### Identity claims

- Identity token does not include any claims by default, except the claim `sub` because it is associated with profile `openid`. This can be changed by setting the `Client.AlwaysIncludeUserClaimsInIdToken = true`, but this can cause the token to become very big, which can be an issue for older browsers if the token is returned through a query string. A better approach is for the client app to issue a request to the `/userinfo` endpoint.
- Calling `/userinfo` endpoint requires an **access token** with scopes relating to the claims to be returned.
- On the client app, define the Open Id Connect middleware with `options.GetClaimsFromUserInfoEndpoint = true`. This way, once the identity token is obtained and validated by the middleware, it will go to the `/userinfo` endpoint and request the user claims. Check the above diagram for more on that.

### Open points

- root folder `tempkey.rsa` - what is that?
