# API Gateway and AuthService

The gateway is the public HTTP entry point for the authentication service and
the FoodSelection microservices:

- `http://localhost:7210/api/FoodProduct/...` is forwarded to FoodService.
- `http://localhost:7210/api/users/...` is forwarded to UserService.
- `POST /api/auth/register` and `POST /api/auth/login` are forwarded to AuthService and issue JWT access tokens.
- AuthService and UserService share the `UserDb.Users` MongoDB collection. AuthService owns registration, password hashes, login, and token issuance; UserService owns user-facing operations and object counters.
- `http://localhost:7210/health` reports gateway health.

Start the application with `docker compose up --build`. The microservices are
available to other containers on the Compose network but do not publish their
HTTP ports to the host.

Set `JWT_SIGNING_KEY` to a private value of at least 32 bytes before starting
Compose. Send the returned token as `Authorization: Bearer <token>` for requests
to `/api/users/...` and `/api/FoodProduct/...`. Registration and login are
public; all business API endpoints require a valid, unexpired token. Passwords
are stored as salted PBKDF2 hashes, not plaintext.

The upstream addresses can be changed with `Services__FoodService` and
`Services__UserService`.
