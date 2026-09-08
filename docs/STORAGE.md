# Supabase Storage contract

The API accesses Supabase Storage only through the Storage HTTP API. It never writes to the `storage` PostgreSQL schema. `SupabaseStorage__ServiceRoleKey` is a server-only secret and must not be exposed to browsers, committed, or logged.

## Buckets

| Purpose | Default bucket | Visibility |
| --- | --- | --- |
| Profile avatars and hero media | `avatars` | Public read, server write/delete |
| Skill icons | `skill-icons` | Public read, server write/delete |
| Project gallery media | `project-images` | Public read, server write/delete |
| Certificate evidence | `certificate-files` | Private, server write/delete and signed read URL |
| Resume/CV documents | `cv-files` | Private, server write/delete and signed read URL |

Bucket names are separately configurable and must be distinct. Create the buckets and their policies in Supabase before enabling uploads. Application tables store object-key identity except `technologies.icon_value`, whose documented image contract stores the managed public URL. The legacy-named `profiles.avatar_url` and `profiles.hero_image_url` columns hold object keys, while API responses derive public URLs from the configured `avatars` bucket. Signed URLs are transient response data and are never durable metadata.

## Object keys and validation

Feature services generate keys from a resource-scoped prefix plus a random UUID and a validated extension, for example `profiles/{profileId}/{uuid}.png`. Client filenames are sanitized for display metadata only and never become object keys.

Allowed file content is verified using all of the following before upload:

- non-zero size and the configured purpose-specific size limit;
- an allowed extension/MIME pair;
- a matching PNG, JPEG, WebP, or PDF signature;
- a readable, seekable stream whose position is restored after inspection.

Skill icons allow PNG, JPEG, WebP, or passive SVG, default to a 512 KiB limit (`SkillIcons__MaxFileSize`), and use `skills/{skillId}/{uuid}.{extension}` keys in `SupabaseStorage__Buckets__SkillIcons`. SVG parsing prohibits DTD/entity declarations, active elements, event attributes, external references, and foreign namespaces. External image URLs supplied through JSON must use HTTPS and a host listed in `SkillIcons__AllowedExternalHosts`.

Hero images additionally enforce `Upload__MaxHeroImageWidth` and `Upload__MaxHeroImageHeight` (both default to 8192 pixels and are capped at 16384).

Project gallery uploads accept PNG, JPEG, and WebP only. Each request contains one or more `files` parts plus a JSON `metadata` part whose zero-based `fileIndex` values map metadata to file order. The default request limit is 10 files (`Upload__MaxProjectGalleryFiles`, capped at 20), every image requires unique non-negative display order plus English and Vietnamese alt text, and generated keys use `projects/{projectId}/{uuid}.{extension}` in `SupabaseStorage__Buckets__ProjectImages`.

Project write responses expose public image URLs, while the database retains object keys in `project_images.image_url` and the selected thumbnail object key in `projects.thumbnail_url`. Removing gallery metadata or deleting a Project commits the database change before attempting object deletion. A multi-file failure compensates every object uploaded by that request.

Certificate evidence uploads accept one PNG, JPEG, WebP, or PDF file up to `Upload__MaxFileSize` (10 MiB by default, capped at 25 MiB). Generated keys use `certificates/{certificateId}/{uuid}.{extension}` in the private `SupabaseStorage__Buckets__CertificateFiles` bucket. PDFs replace `certificates.file_url`; images replace `certificates.image_url`, so a certificate may retain one file and one image independently. API responses convert those object keys into `downloadUrl` and `imageUrl` values that expire after five minutes; neither object keys nor signed URLs are persisted or logged.

Deleting a Certificate commits the metadata cascade before deleting both private objects. A failed metadata update compensates the newly uploaded object, and the previous object is deleted only after the replacement key commits.

## Replacement lifecycle

The required order is:

1. upload the new object;
2. persist the new bucket/object key;
3. if persistence fails, attempt to delete the new object and preserve the persistence error;
4. after persistence commits, delete the old object;
5. if compensating deletion fails, emit a structured reconciliation-required error without hiding the persistence failure.

Storage requests have a bounded timeout and bounded response-body parsing. Provider response bodies and the service-role key are not included in application exceptions.
