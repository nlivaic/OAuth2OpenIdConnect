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

- JWT structure:
  - [Specification](https://tools.ietf.org/html/rfc7519)

##### Identity Token

- Identity token is obtained through authentication via the authorization endpoint at IDP.
- Identity token is used to create a claims identity (`ClaimsPrincipal User`).
- Relevant standard claims:
  - `sub` - user identifier, returned if using Open Id Connect (signalled by the `openid` profile).
  - `aud` - client app identifier
  - `iss` - issuing authority, the IDP.
  - `iat` - time the JWT was issued at. Unix time.
  - `exp` - time on or after the JWT is expired. Unix time.
  - `nbf` - not before, signifies the time before which the JWT is not valid for processing. Unix time.
  - `auth_time` - time of the original authentication. Unix time.
  - `amr` - authentication method references. An array of identifiers for authentication methods. E.g. "pwd" for password.
  - `nonce` - number only used once. Generated on the client, sent back by the IDP to prevent CSRF attacks.
  - `at-hash` - access token hash value, linking this specific identity token to the access token.

##### Access token

- Access token authorizes the client application to access an API.

#### Relevant endpoints

##### Client

- `/signin-oidc`
- `/signout-callback-oidc`

##### IDP

- `/.well-known/openid-configuration` - discovery document with all the other endpoints. The only endpoint in this section that is guaranteed to be found on this URI. Others below could be relative to some other URI. If you need to talk to any of the below endpoints, it is best to first call discovery document and fetch URIs from there.
- `/authorize` - authorization endpoint
- `/token` - token endpoint
- `/endsession` - deletes the authentication token on IDP
- `/userinfo` - identity claims for the logged in user

#### Claims

- Claims Principal is the logged in user.
- Claims Identity is a set of claims for the logged in user. A Claims Principal can have multiple sets of Claims Identity, each set coming from another source. E.g. multi-factor authentication, with each factor asserting its own claims. Or another example, client app receives some claims via the IDP and after authenticating the user fetches an additional set of claims from an internal database.

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
- `response_type=code`

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

- Since the identity token is obtained through back-channel communication, you cannot see it in Chrome or any of the logs. You can get the token from your controller by calling `HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken)` and writing it out to the debug output. For more on this take a look at [GalleryController.WriteOutIdentityInformation](src\ImageGallery.Client\Controllers\GalleryController.cs).
- Now that you have the token, you can make sure the claims identity user has been created from the token at hand by comparing the token claim `sub` and claim `nameidentifier` from the `User` object. Both should be the same. Now, the reason we are looking at two differently named claims is discussed below at [Claim Transformation](#claim-transformation).

#### Authorization Code Injection Attack

- If the attacker gets a hold of the users authorization code, he can swap the user's browser session with his own. This way the attacker now has the victim's privileges. Details can be found [here](https://tools.ietf.org/html/draft-ietf-oauth-security-topics-14#page-21).
- Advised approach is to utilize PKCE (Proof Key for Code Exchange) when going with the authorization code flow. ![PKCE](https://user-images.githubusercontent.com/26722936/79879157-a0b6a180-83ee-11ea-9523-1e1ee3a9f771.png)
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

### Claims

- Can be used for identity-related information and for authorization.
- ## Open Id Connect middleware and Open Id standard have a way of determining which claims are requested. More on this in the [Claims Transformation](#claims-transformation) subsection below.

#### Identity Claims

- Identity token does not include any claims by default, except the claim `sub` because it is associated with profile `openid`. This can be changed by setting the `Client.AlwaysIncludeUserClaimsInIdToken = true`, but this can cause the token to become very big, which can be an issue for older browsers if the token is returned through a query string. A better approach is for the client app to issue a request to the `/userinfo` endpoint.
- Calling `/userinfo` endpoint requires an **access token** with scopes relating to the claims to be returned.
- On the client app, define Open Id Connect middleware with `options.GetClaimsFromUserInfoEndpoint = true`. This way, once the identity token is obtained and validated by the middleware, the middleware will go to the `/userinfo` endpoint and request the user claims. Check the above diagram for more on that.
- Calling `/userinfo` manually:
  - We might do this so as to keep private data out of the authentication cookie, to keep the cookie small and to have up-to-date data.
  - Such a request is a GET (but can be POST as well), with access token as a Bearer token. Such a call will return claims related to scopes in the access token.
  - For more information and a demo, take a look at `GalleryController.OrderFrame` method [here](src\ImageGallery.Client\Controllers\GalleryController.cs). Bear in mind you have to import an `IdentityModel` package.

#### Role-based Authorization

#### Claim and Profiles

- This section describes which claims the IDP will return (either by default or in relation to the requested scopes). We will also talk about how client app Open Id Connect middleware filters and/or maps claims to other claims.
- Some claims are default according to the Open Id standard and are always returned from IDP even though they were not requested. Such claims are not filtered out by Open Id Connect middleware. They can be explicitly filtered out when setting up Open Id Connect middleware. Default claims are e.g. `sid`, `idp`, `auth_time` etc.
- Some claims are requested by the Open Id Connect middleware even though the middleware has not been explicitly defined to ask for those profiles. Specifically, these profiles are `openid` and `profile`. `openid` profile returns `sub` claims while `profile` profile returns `given_name`, `family_name`. Open Id Connect middleware has explicit mappings for such claims, e.g. `sub`, `given_name`, `family_name`.
- All the other claims out there are either explicitly filtered out by the Open Id Connect middleware or not mentioned at all (and thus filtered out).
- To sum up, default claims and claims scoped `openid` and `profile` are received without any explicit requesting by the client app (if the client app is using the Open Id Connect middleware). Some of the received claims are then filtered out on the client app. To fetch claims belonging to any other scopes (e.g. `address` scope) the Open Id Connect middleware will need to be explicitly defined so and even then will need explicit mapping on the to get into the claims identity collection.
- Further, claims received from IDP are transformed according to a dictionary consulted by Open Id Connect middleware. This can be observed when the `sub` claim is transformed to `nameidentifier`. In order to skip the transformation we must clear the dictionary. This is done in `Startup` constructor by calling `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`.
- Some of the claims are filtered automatically by the middleware (e.g. `nbf`). Removing this **filter** is done by configuring Open Id Connect middleware with `options.ClaimActions.Remove("nbf")`, thus allowing `nbf` to get through to the client app.
- Some of the claims (e.g. `idp` and `sid`) are not needed in the client app, but are not filtered automatically. Keeping them around makes authentication cookie larger than necessary and we have no need for those claims. To filter these out we must configure Open Id Connect middleware with `options.ClaimActions.DeleteClaim("sid")`. Claims to filter out explicitly on client app: `sid`, `idp`, `auth_time`, `s_hash`.
- And yes, the filter naming above is confusing.
- You can also map claim type from the token to another type in the claims collection.
- It is advisable not to explicitly filter out claim `amr`, since some applications might allow or block certain functionalities, depending on how strong the authentication method was.
- Access token and profiles: authorization code flow gets scoped to profiles requested in the initial authentication request. These profiles are then written to both identity token and access token. On any subsequent requests e.g. to the `/userinfo` endpoint just send the access token, it is already scoped to appropriate profiles. You cannot add profiles on the fly.

### Hybrid Flow

- Similar to authorization code flow, but identity token is returned alongside authorization code via the front-channel. Authorization code is still exchanged via the back-channel for the identity token. Both identity tokens are then compared for equality, this way attacks are mitigated.
- Downside here is the identity token is received via the front-channel, potentially leaking personally identifiable information. Another downside is the client-side implementation of attack mitigation is more complex to implement, while with authorization code flow the client only has to generate a PKCE code verifier.
- Conclusion: even though the hybrid flow is a secure option, rather use authorization code for ease of use.

### Open Points

- root folder `tempkey.rsa` - what is that?
