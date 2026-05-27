# Local production secrets

Create one file per configuration key before running `deploy/docker-compose.prod.yml`.

Required files:

- `ConnectionStrings__DefaultConnection`
- `Storage__S3__BucketName`
- `Storage__S3__Region`
- `Storage__S3__AccessKey`
- `Storage__S3__SecretKey`

Optional files:

- `Storage__S3__ServiceUrl`
- `Storage__S3__PublicBaseUrl`
- `Authentication__CookieName`

The app converts double underscores in file names to configuration sections.
