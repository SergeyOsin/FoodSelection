# API Gateway and AuthService

Ocelot is the public HTTP entry point for the authentication service and the
FoodSelection microservices:

- `http://localhost:7210/api/FoodProduct/...` is forwarded to FoodService.
- `http://localhost:7210/api/users/...` is forwarded to UserService.
- FoodService is also available directly at `http://localhost:7212`.
- UserService is also available directly at `http://localhost:7211`.
- `POST /api/auth/register` and `POST /api/auth/login` are forwarded to AuthService.
- `GET /health` reports gateway health.
- AuthService and UserService share the `UserDb.Users` MongoDB collection.

Routes and downstream hosts are configured in `ocelot.json`. Both microservices
validate JWTs independently, including requests made directly to their published
ports. FoodService forwards the caller's bearer token when checking a user with
UserService.

Set `JWT_SIGNING_KEY` to a private value of at least 32 bytes before starting
Compose. Registration and login are public. Requests to `/api/users/...` and
`/api/FoodProduct/...` require a valid bearer token in the request's
authorization header, whether accessed through the gateway or directly.

Run the JWT integration tests against the running Compose stack with:
`RUN_ASYNC_CONFIRMATION_INTEGRATION=1 JWT_SIGNING_KEY="$JWT_SIGNING_KEY" dotnet test FoodSelectionAPITests/FoodSelectionAPITest.csproj --filter FullyQualifiedName~JwtAuthorizationTests`.
