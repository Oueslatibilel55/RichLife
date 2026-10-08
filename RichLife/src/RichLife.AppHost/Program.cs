using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var pgUser = builder.AddParameter("pg-user", secret: false);
var pgPassword = builder.AddParameter("pg-password", secret: true);

var postgres = builder
    .AddPostgres("postgres", userName: pgUser, password: pgPassword)
    // A fixed host port, so `dotnet ef database update` and any standalone run of the
    // API can reach the same database with a static connection string. The `port:`
    // argument on AddPostgres does not pin it — the container still gets a random
    // host port without this.
    .WithHostPort(62749)
    .WithDataVolume("richlife-pgdata")
    // One container, reused for the life of the project. Aspire's default is ephemeral:
    // a new container per run with a random suffix, removed by DCP on a clean shutdown.
    // That assumption does not survive a force-kill, a crash or a reboot, so every such
    // run leaks a stopped container and the list grows without bound. Persistent means
    // the container is created once, reused on every later run, and never torn down —
    // so there is nothing to leak. The explicit name keeps it recognisable in
    // `docker ps` instead of a random suffix.
    .WithContainerName("richlife-postgres-aspire")
    .WithLifetime(ContainerLifetime.Persistent);

// No .WithPgAdmin() here on purpose: it exited 255 on every single run and leaked one
// dead container each time. docker-compose already provides pgAdmin on the same 5050.

var richlifeDb = postgres.AddDatabase("richlife");

builder.AddProject<Projects.RichLife_Api>("api")
    .WithReference(richlifeDb)
    .WaitFor(richlifeDb)
    // Pin the API to 5187. launchSettings.json already says 5187, but that only fixes
    // the port Aspire's PROXY listens on — the API itself is handed a fresh random
    // TargetPort on every run. `IsProxied = false` removes the proxy entirely, so the
    // API binds 5187 directly and the address stays stable across restarts. The Angular
    // client hardcodes this port and CORS only allows http://localhost:4200, so a
    // moving port breaks the frontend every time.
    .WithEndpoint("http", endpoint =>
    {
        endpoint.Port = 5187;
        endpoint.TargetPort = 5187;
        endpoint.IsProxied = false;
    })
    .WithExternalHttpEndpoints();

// pgAdmin is not managed by Aspire — it is the docker-compose `richlife-pgadmin`
// service on 5050, with the Aspire database pre-registered. Declaring it as an
// external service puts its tile back on the dashboard (clickable, health-checked)
// without Aspire owning the container. `.WithPgAdmin()` used to provide that tile,
// but its container exited 255 on every run and leaked one dead container each time.
builder.AddExternalService("pgadmin", "http://localhost:5050")
    .WithHttpHealthCheck("/login");

builder.Build().Run();
