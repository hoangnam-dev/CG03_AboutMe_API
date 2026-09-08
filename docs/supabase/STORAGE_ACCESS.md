# Supabase Storage Access Policy

The API is the only writer and deleter. It uses the server-held service-role credential, which must never be sent to a browser, placed in a public environment variable, or written to logs. Application code uses the Storage HTTP API and never inserts, updates, or deletes rows in the managed `storage` schema.

## Required bucket matrix

| Bucket | Public read | Anonymous/authenticated write | API service-role write/delete | Application access |
| --- | --- | --- | --- | --- |
| `avatars` | Yes | No | Yes | Stable public URL |
| `project-images` | Yes | No | Yes | Stable public URL |
| `skill-icons` | Yes | No | Yes | Stable public URL |
| `certificate-files` | No | No | Yes | Five-minute signed URL |
| `cv-files` | No | No | Yes | Five-minute signed URL |

## Dashboard procedure

1. In Storage, create each bucket with the exact name above. Set only the first three buckets to Public.
2. Set per-bucket provider file-size limits at or below the corresponding backend limits. Keep allowed MIME types aligned with the backend: images for public media, PDF/images for certificate evidence, and PDF for CV files.
3. In Storage policies, remove every `INSERT`, `UPDATE`, and `DELETE` policy granted to `anon` or `authenticated` for these buckets. Do not add client write policies; service-role requests bypass Storage RLS.
4. Do not add `SELECT` policies for the private buckets. Reads are issued only as short-lived signed URLs by the API.
5. Rotate the service-role key after any suspected exposure and update only the deployment secret `SupabaseStorage__ServiceRoleKey`.

## Production-equivalent smoke test

Use a unique harmless object under `security-smoke/<uuid>.txt` and delete it after the run.

1. With no authorization header, verify public URLs in `avatars`, `project-images`, and `skill-icons` return the test object, while direct URLs in `certificate-files` and `cv-files` return 400/401/403/404.
2. With Supabase `anon` and an ordinary authenticated JWT, verify upload, overwrite, and delete requests fail for all five buckets.
3. Through the deployed API, upload one allowed public image and one allowed private PDF. Verify the public URL works and the private response contains a signed URL but no object key.
4. Wait beyond the configured five-minute private URL lifetime and verify the old signed URL fails.
5. Delete the records through the API and verify the corresponding objects are removed. Search captured application logs for the service-role key, bearer token, signed query string, submitted content, and connection string; every search must return zero matches.

Record the project, timestamp, tester, and pass/fail result in the release ticket. Never paste credentials, signed URLs, or object contents into that ticket.

## Rollback

Bucket visibility is rolled back through the dashboard by restoring the pre-change Public toggle captured in the release ticket. Remove any policies added during the change by their exact policy name. Do not create permissive replacement policies as a shortcut. Rotating a service-role key is not rolled back; deploy a new key if rollback requires another credential change.

