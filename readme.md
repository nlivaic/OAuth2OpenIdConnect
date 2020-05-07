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
  - Segments:
    - Header - metadata, signing algorithm.
    - Payload - various claims.
    - Signature - first two segments signed by the IDP using the signing algorithm.

##### Identity Token

- JWT exclusively.
- Identity token is obtained through authentication via the authorization endpoint at IDP.
- Identity token is used to create a claims identity (`ClaimsPrincipal User`) on the client app.
- Only default claims and claims relating to `openid` scope are in the identity token. Claims belonging to other requested scopes (e.g. `profile`, `roles` or `address` scopes) are not included in identity token.
- Relevant standard claims:
  - `sub` - user identifier, returned if using Open Id Connect (signalled by the `openid` scope).
  - `aud` - client app identifier
  - `iss` - issuing authority, the IDP.
  - `iat` - time the JWT was issued at. Unix time.
  - `exp` - time on or after the JWT is expired. Unix time.
  - `nbf` - not before, signifies the time before which the JWT is not valid for processing. Unix time.
  - `auth_time` - time of the original authentication. Unix time.
  - `amr` - authentication method references. An array of identifiers for authentication methods. E.g. "pwd" for password.
  - `nonce` - number only used once. Generated on the client, sent back by the IDP to prevent CSRF attacks.
  - `at_hash` - access token hash value, linking this specific identity token to the access token.
- Default lifetime 5 minutes.

##### Access token

- Usually a JWT, but not necessarily.
- Sent as Bearer token with each request to the API.
- Access token authorizes the client application to access an API.
- It is returned by the authorization endpoint.
- Identity token has a claim `at_hash` linking the access token. Client app does limited validation by calculating access token's hash value and comparing to the identity tokens `at_hash`.
- API also validates the access token.
- Relevant claims:
  - `sub` - user identifier, returned if using Open Id Connect (signaled by the `openid` scope).
  - `iss` - issuing authority, the IDP.
  - `aud` - the intended audience, i.e. names of API scopes representing APIs that can be accessed with this access token. So, to access an Image Gallery API, the `aud` must say `imagegalleryapi` and the access token must be scoped to `imagegalleryapi`. Optionally, IDP might also include itself in the list (so client app can access e.g. `/userinfo`), but nowadays implementations will allow accessing the issuer by default.
  - `client_id` - client app identifier the access token was issued to.
  - `scope` - various scopes access token is scoped to, e.g. `openid profile imagegalleryapi`.
- Default lifetime 60 minutes.

##### Refresh token

- A credential used to get new tokens.
- Client must request "offline_access" flow to enable refresh tokens.
- Default lifetime 30 days.
- More [below](#refresh-tokens)

##### Authentication System

![Authentication System](https://user-images.githubusercontent.com/26722936/80860699-b12a1000-8c69-11ea-9d68-b917d8afdcbb.png)

##### Reference token

- An identifier token.
- It is a type of access token. Resource servers exchange it with every request for an up-to-date access token.
- Sent to an introspection endpoint.
- More [below](#-reference-tokens).

#### Relevant endpoints

##### Client

- `/signin-oidc`
- `/signout-callback-oidc`

##### IDP

- `/.well-known/openid-configuration` - discovery document with all the other endpoints. The only endpoint in this section that is guaranteed to be found on this URI. Others below could be relative to some other URI. If you need to talk to any of the below endpoints, it is best to first call discovery document and fetch URIs from there.
- `/authorize` - authorization endpoint
- `/token` - token endpoint, used to exchange authorization code for identity token, access token and refresh token (optional). It is also used to exchange refresh token for new identity token, access token and refresh token.
- `/endsession` - deletes the authentication token on IDP
- `/userinfo` - identity claims for user. Accessed with access token. Intended to be called only by client app, not the APIs.
- `/introspect` - introspection endpoint used to exchange reference tokens for access tokens. Called by resource server. Requires authorization by the API (client_id, client_secret).

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
  - In [Config.cs](src\Marvin.IDP\Config.cs) you can provide first-hand configuration to get started with development efforts:
    - Define Identity resources (a.k.a. scopes) that this Identity Server will provide.
    - Api resources - Apis that will work with this Identity Server's tokens.
    - Client resources - Clients and their details: client ids, client secrets, allowed scopes, allowed grants, PKCE, callback Uri...
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
  - Call into `/userinfo` endpoint (optional).
  - Handle identity token validation.
- Add an encrypted authentication cookie with `.AddCookie()`.
- By default the OpenIdConnect middleware is defined with the sign in callback endpoint at `/signin-oidc`. This can be overridden by providing a value to the `CallbackPath`.
- Use the above authentication and authorization services as part of authentication and authorization middleware by calling `app.UseAuthentication()` and `app.UseAuthorization()`.

### Authorization code flow

#### Flowchart

![Authorization code flow with PKCE protection](https://user-images.githubusercontent.com/26722936/80860693-a8d1d500-8c69-11ea-99ec-cb060b647cfc.png)

#### Outline

- `response_type=code`
- Open Id Connect and OAuth2.
- Utilizes front-channel and back-channel communication.
- Authorization code is a short-lived proof of who the user is. It binds the front-end session between the user and the client with the back-end session between the client server and IDP. It is obtanied from the authorization endpoint. Has a default lifetime of 5 minutes.
- Identity token is obtained from the token endpoint. In it are claims used by the server to create a claims identity and log the user in using an encrypted cookie.
- Access token is used as a Bearer token to access APIs.

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
  - If fetching user claims from the `/userinfo` endpoint, client app passes the access token to the endpoint. Endpoint validates the token and returns the claims associated with the scopes in the access token. Please note there is more on this in the [Identity Claims](#identity-claims) section.
  - Client app creates a claims identity (user). This is what is accessible through `Controller.User`.
  - Authentication ticket is created, encrypted and stored in an encrypted cookie.
- Above steps are facilitated through the use of the Authentication and OpenIdConnect middlewares, but could also be done manually.

#### Checking the token for yourself.

- Since the identity token is obtained through back-channel communication, you cannot see it in Chrome or any of the logs. You can get the token from your controller by calling `HttpContext.GetTokenAsync(OpenIdConnectParameterNames.IdToken)` and writing it out to the debug output. For more on this take a look at [GalleryController.WriteOutIdentityInformation](src\ImageGallery.Client\Controllers\GalleryController.cs).
- Now that you have the token, you can make sure the claims identity user has been created from the token at hand by comparing the token claim `sub` and claim `nameidentifier` from the `User` object. Both should be the same. Now, the reason we are looking at two differently named claims is discussed below at [Claim Transformation](#claim-transformation).

#### Authorization Code Injection Attack

- If the attacker gets a hold of the users authorization code, he can swap the user's browser session with his own. This way the attacker now has the victim's privileges. Details can be found [here](https://tools.ietf.org/html/draft-ietf-oauth-security-topics-14#page-21).
- Advised approach is to utilize PKCE (Proof Key for Code Exchange) when going with the authorization code flow.
- Setup:
  - At the IDP, you enable it per client, by adding a `RequirePkce = true`.
  - At the client app, configure the `OpenIdConnect` middleware with `options.UsePkce = true` (or just omit the line, since the middleware defaults to true).

#### Logout

- Sign out of the client app by deleting the authentication cookie. This is done by calling `HttpContext.SignOutAsync(CookieAuthenticationScheme.CookieAuthenticationDefaults.LogScheme)`.
- It is not enough to simply log out of the client app, as we will simply keep getting redirected back to the IDP. Since we are still logged in the IDP, it will redirect us back to the client app with a new token. We must also log out of the IDP because that is the only way IDP's authentication cookie can get deleted. This is done by calling `HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme)`. Calling the `SignOutAsync` will redirect to the end session endpoint at the IDP (`/connect/endsession`) causing the IDP's authentication cookie to get deleted. Also note the token itself was sent to the IDP (in the query string, `id_token_hint`) so the IDP can double check who is doing the logout and prevent any attacks.
- Once both of the above are set up, we are logged out of both the client app and IDP. However, two issues exist here. First, if you take a look at the IDP output, you will notice a warning saying "_Invalid PostLogoutRedirectUri_". Second, we are then stuck at the IDP's logged out page, which is not user-friendly. We should tell our app and the IDP where to redirect after the sign out. Both of these points are addressed below:
  - Client app: by default the OpenIdConnect middleware defines the sign out callback endpoint at `/signout-callback-oidc`. This can be overridden by providing a value to the `SignedOutCallbackPath`. In any case, sign out callback URI is sent to the IDP on logout redirect as `post_logout_redirect_uri` query string.
  - IDP: same value **must** be defined on the IDP side as well, per client. Callback URI is provided to the `PostLogoutRedirectUris`, you cannot rely on default URIs here. Because we didn't provide this value earlier, the above "_Invalid PostLogoutRedirectUri_" warning was issued by the IDP.
  - IDP: at this point the IDP does not issue any warnings since callback URIs are sorted out. However, it still does not REDIRECT to the callback URI. This must be manually enabled by setting `AccountOptions.AutomaticRedirectAfterSignOut = true`.

### Securing your API

- At IDP:
  - Add a new API resource. This is a scope that represents access to the API.
  - Allow the above scope for the client app.
- Client app (Open Id Connect middleware):
  - If using authorization code flow, access token is requested as part of it (`response_type=code`).
  - Add the scope to the list of requested scopes.
  - Create a delegating handler that adds a bearer token with each request, as in [BearerTokenHandler.cs](src\ImageGallery.Client\HttpHandlers\Startup.cs). Register the handler with the HttpClient factory in [Startup.cs](src\ImageGallery.Client\Startup.cs) using `AddHttpMessageHandler<BearerTokenHandler>()`. Don't forget to register `<BearerTokenHandler>` and `<IHttpContextAccessor, HttpContextAccessor>` as well.
- API:
  - Install package `IdentityServer4.AccessTokenValidation`.
  - Configure services that will validate the access token, as in [Startup.cs](src\ImageGallery.API\Startup.cs) `services.AddAuthentication(...).AddIdentityServerAuthentication(...)`.
  - Add authentication and authorization middleware, as usual. Put it after routing middleware.

### Claims

- Can be used for identity-related information and for authorization.
- Open Id Connect middleware and Open Id standard have a way of determining which claims are requested. More on this in the [Claims Transformation](#claims-transformation) subsection below.

#### Identity Claims

- Identity token does not include **any** claims by default, except the claim `sub` because it is associated with scope `openid`. So, `given_name`, `family_name`, `address`, `role` etc are not in the identity token. This can be changed by setting the `Client.AlwaysIncludeUserClaimsInIdToken = true`, but this can cause the token to become very big, which can be an issue for older browsers if the token is returned through a query string. A better approach is for the client app to issue a request to the `/userinfo` endpoint.
- Calling `/userinfo` endpoint requires an **access token** with scopes relating to the claims to be returned.
- On the client app, define Open Id Connect middleware with `options.GetClaimsFromUserInfoEndpoint = true`. This way, once the identity token is obtained and validated by the middleware, the middleware will go to the `/userinfo` endpoint and request the user claims. Check the above diagram for more on that.
- You can also call `/userinfo` manually from your code:
  - We might do this so as to keep private data out of the authentication cookie, to keep the cookie small and to have up-to-date data.
  - Such a request is a GET (but can be POST as well), with access token as a Bearer token. Such a call will return claims related to scopes in the access token.
  - For more information and a demo, take a look at `GalleryController.OrderFrame` method [here](src\ImageGallery.Client\Controllers\GalleryController.cs). Bear in mind you have to import an `IdentityModel` package.

#### Role-based Authorization

- `[Authorize(Roles = "role1,role2,...")]`
- Role is just another claim.
- Can be applied on both the client app and API.
- Client app:
  - To make the .NET Claims Principal aware of a role, you must first fetch the role claim from the IDP (via `/userinfo`) and tell the Open Id Connect middleware which claim relates to a role concept: `options.TokenValidationParameters = new TokenValidationParameters { ..., RoleClaimType = JwtClaimTypes.Role }`. You can add different claims this way. The point of doing it this way is to have access to roles from your code:
    - To be able to call `User.IsInRole()`, which is much more user-friendly than poking aroung the `role` claim values.
    - Call `[Authorize(Roles="role1,role2...")]`
- IDP:
  - First of all, we need to get a role claim into the access token. This is a good way to go because otherwise the API will have to talk to `\userinfo` every time a request comes in, and that would be expensive. We do this by telling the IDP we want the `role` claim included in the access token along with the `imagegalleryapi` scope: `new ApiResource(..., new List<string> { claim here } )`.
- API:
  - Then, protect the API controller action with `[Authorize(Roles = "role1,role2,...")]`.

#### Claim and Scopes

- This section describes which claims the IDP will return (either by default or in relation to the requested scopes). We will also talk about how client app Open Id Connect middleware filters and/or maps claims to other claims.
- Some claims are default according to the Open Id standard and are always returned from IDP even though they were not requested. Such claims are not filtered out by Open Id Connect middleware. They can be explicitly filtered out when setting up Open Id Connect middleware. Default claims are e.g. `sid`, `idp`, `auth_time` etc.
- Some claims are requested by the Open Id Connect middleware even though the middleware has not been explicitly defined to ask for those scopes. Specifically, these scopes are `openid` and `profile`. `openid` scope returns `sub` claims while `profile` scope returns `given_name`, `family_name`. Open Id Connect middleware has explicit mappings for such claims, e.g. `sub`, `given_name`, `family_name`.
- All the other claims out there are either explicitly filtered out by the Open Id Connect middleware or not mentioned at all (and thus filtered out).
- To sum up, default claims and claims scoped `openid` and `profile` are received without any explicit requesting by the client app (if the client app is using the Open Id Connect middleware). Some of the received claims are then filtered out on the client app. To fetch claims belonging to any other scopes (e.g. `address` scope) the Open Id Connect middleware will need to be explicitly defined so and even then will need explicit mapping on the to get into the claims identity collection.
- Further, claims received from IDP are transformed according to a dictionary consulted by Open Id Connect middleware. This can be observed when the `sub` claim is transformed to `nameidentifier`. In order to skip the transformation we must clear the dictionary. This is done in `Startup` constructor by calling `JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear()`.
- Some of the claims are filtered automatically by the middleware (e.g. `nbf`). Removing this **filter** is done by configuring Open Id Connect middleware with `options.ClaimActions.Remove("nbf")`, thus allowing `nbf` to get through to the client app.
- Some of the claims (e.g. `idp` and `sid`) are not needed in the client app, but are not filtered automatically. Keeping them around makes authentication cookie larger than necessary and we have no need for those claims. To filter these out we must configure Open Id Connect middleware with `options.ClaimActions.DeleteClaim("sid")`. Claims to filter out explicitly on client app: `sid`, `idp`, `auth_time`, `s_hash`.
- And yes, the filter naming above is confusing.
- You can also map claim type from the token to another type in the claims collection.
- It is advisable not to explicitly filter out claim `amr`, since some applications might allow or block certain functionalities, depending on how strong the authentication method was.

##### Adding a new claim and a new scope

- On IDP:
  - Add a new claim to the `User`: `TestUser.Claims = { ..., new Claim(...) }`.
  - Add a new scope and map it to the new claim in [Config.cs](src\Marvin.IDP\Config.cs): `new IdentityResource(...)`.
  - Allow the client access to the new scope: `Client.AllowedScopes = ...`.
- On client:
  - Since this is a custom claim scoped other than `openid profile`, we must map it manually. Define Open Id Connect middleware with `options.ClaimsActions.MapUniqueJsonKey()`.

#### Policy-based authorization

- Binds multiple claims together into a policy.
- A.k.a. Attribute-based authorization.
- A role is still a claim, but with policies there is no need for roles as authorization is expressed in a different manner using policies.
- Check out `.AddAuthorization(authorizationOptions => authorizationOptions.AddOptions(...) )` in [Startup.cs](src\ImageGallery.Client\Startup.cs) for a basic case involving several claims and demanding the user to be authenticated.
- A more complex case might require calling into the database, reading and comparing claim values, accessing HttpContext to read route data etc. This can be done by using `IAuthorizationRequirement` marker interface and `AuthorizationHandler<T>`. Checkout out `.AddAuthorization(authorizationOptions => authorizationOptions.AddOptions(...) )` [Startup.cs](src\ImageGallery.API\Startup.cs), [MustOwnImageRequirement.cs](src\ImageGallery.API\Authorization\MustOwnImageRequirement.cs) and [MustOwnImageHandler.cs](src\ImageGallery.API\Authorization\MustOwnImageHandler.cs).

### Refresh tokens

![Refresh token flow](https://user-images.githubusercontent.com/26722936/80861942-e63a6080-8c71-11ea-995a-f92128220a1c.png)

- A credential used to get new tokens.
- New tokens are received from the token endpoint. All tokens are refreshed: identity token, access token and refresh token.
- Client must request `offline_access` flow to enable refresh tokens. "Offline" as used here means providing access to client app and API even when not logged into (a.k.a. offline) IDP. Don't forget to allow the client to request the `offline_access` scope.
- At IDP:
  - `Client.AllowOfflineAccess = true;` - enables client to request `offline_access` scope.
  - `Client.AbsoluteRefreshTokenLifetime = ...;` - 30 days by default.
  - `Client.RefreshTokenExpiration = TokenExpiration.Sliding; Client.SlidingRefreshTokenLifetime = ...;`. This is optional. Renews refresh token expiration date with each refresh, but total time cannot be larger than absolute refresh token lifetime (see above, 30 days by default).
  - `Client.UpdateAccessTokenClaimsOnRefresh = true;` - by default, claims in the access token are not updated when the access token is refreshed for the duration of access token lifetime. Only when the refresh token expires (and the user authenticates again) is the access token refreshed. This property can be used to force claims update on every access token refresh.
- Before calling the API, read the access token expiration value (`expires_at` claim) and if it has expired or is nearing expiration, client app should extract the refresh token and talk to the IDP to refresh the access token. Then client app should store the newly received id_token, access_token, refresh_token and `expires_at` claim (which can be stored as a token as well) and then sign in again, thus persisting all the tokens in the authentication cookie. This has been done in [BearerTokenHandler.cs](src\ImageGallery.Client\HttpHandlers\BearerTokenHandler.cs).
- Please note: Open Id Connect middleware has a 5 minute skew, the function of which is to take into account small time differences between IDP and APIs (e.g. token's `nbf` is a few minutes after the API servers current time, which would result in the access token being reject if not for the skew).
- Seeing refresh tokens in action: waiting an hour for the access token to expire is too long. In the sample app, set a few values as below:
  - In the client app: `BearerTokenHandler.GetAccessToken()`, variable `timeBeforeExpiration = 30`
  - At the IDP: `AccessTokenLifetime = 120; AbsoluteRefreshTokenLifetime = 600;`

### Reference tokens

![Reference tokens](https://user-images.githubusercontent.com/26722936/81301289-25edb780-9079-11ea-99cf-0f9501a32f71.png)

- Token format standing opposite of self-contained token format.
- Resource servers exchange the reference token for an up-to-date access token, via the back-channel communication with the introspection endpoint. Resource server must authenticate with the IDP by sending the id and secret (as headers). This means the IDP must define a secret for the resource.
- Pros: better access token lifetime management, access tokens are more secure since they are used only once.
- Cons: resource server must communicate with the IDP on every request.
- Can be combined with refresh tokens. It appears reference tokens also have an expiration date, which is set on the IDP in the same manner as the access token's expiration date.
- Resource server exchanges reference token for access token via introspection endpoint.
- How to enable on IDP:
  - `Client.AccessTokenType = AccessTokenType.Reference` - turns access token into a reference format.
  - Define a secret for the API resource.
- How to enable on API:
  - Tell the authentication middleware your client secret.

### Other flows

- Implicit flow - via front-channel communication.
- Resource owner password credentials - included for legacy reasons, should be avoided.
- Client flow - only for machine to machine communication.
- Hybrid flow:
  - Open Id Connect only.
  - Similar to authorization code flow, but identity token is returned alongside authorization code via the front-channel. Authorization code is still exchanged via the back-channel for the identity token. Both identity tokens are then compared for equality, this way attacks are mitigated.
  - Downside here is the identity token is received via the front-channel, potentially leaking personally identifiable information. Another downside is the client-side implementation of attack mitigation is more complex to implement, while with authorization code flow the client only has to generate a PKCE code verifier.
  - Conclusion: even though the hybrid flow is a secure option, rather use authorization code for ease of use.

### Open Points

- root folder `tempkey.rsa` - what is that?
