using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Portfolio.Application.About;
using Portfolio.Application.Authentication;
using Portfolio.Application.Certificates;
using Portfolio.Application.Experiences;
using Portfolio.Application.Profiles;
using Portfolio.Application.Projects;
using Portfolio.Application.Resumes;
using Portfolio.Application.Skills;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Portfolio.Api.OpenApi;

public sealed class RequestExampleOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (operation.RequestBody?.Content is not { } content)
        {
            return;
        }

        var requestType = context.MethodInfo.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .FirstOrDefault(type => CreateExample(type) is not null);
        if (requestType is null)
        {
            return;
        }

        foreach (var mediaType in content.Values)
        {
            mediaType.Example = CreateExample(requestType);
        }
    }

    private static JsonNode? CreateExample(Type requestType) => requestType switch
    {
        _ when requestType == typeof(LoginRequest) => Parse(
            """
            {
              "email": "admin@example.com",
              "password": "not-logged-or-echoed"
            }
            """),
        _ when requestType == typeof(ProfileUpdateRequest) => Parse(
            """
            {
              "slug": "nguyen-hoang-nam",
              "fullName": "Nguyen Hoang Nam",
              "email": "admin@example.com",
              "phone": "+84901234567",
              "showEmail": false,
              "showPhone": false,
              "availableForWork": true,
              "translations": {
                "en": {
                  "title": "Backend Engineer",
                  "shortBio": "Backend developer focused on reliable APIs.",
                  "location": "Ho Chi Minh City",
                  "availability": "Available for selected projects"
                },
                "vi": {
                  "title": "Ky su Backend",
                  "shortBio": "Lap trinh vien Backend tap trung vao API on dinh.",
                  "location": "Thanh pho Ho Chi Minh",
                  "availability": "San sang cho du an phu hop"
                }
              },
              "socialLinks": [
                {
                  "platform": "github",
                  "label": "GitHub",
                  "url": "https://github.com/example",
                  "iconName": "github",
                  "displayOrder": 0,
                  "isPublished": true
                }
              ]
            }
            """),
        _ when requestType == typeof(AboutUpdateRequest) => Parse(
            """
            {
              "yearsOfExperience": 2,
              "projectCount": 3,
              "technologyCount": 15,
              "showYearsOfExperience": true,
              "showProjectCount": true,
              "showTechnologyCount": true,
              "showContactSection": true,
              "isPublished": true,
              "translations": {
                "en": {
                  "content": "Backend developer focused on reliable web APIs.",
                  "careerGoal": "Build secure and maintainable software."
                },
                "vi": {
                  "content": "Lap trinh vien Backend tap trung vao API web on dinh.",
                  "careerGoal": "Xay dung phan mem an toan va de bao tri."
                }
              }
            }
            """),
        _ when requestType == typeof(SkillCategoryWriteRequest) => Parse(
            """
            {
              "displayOrder": 0,
              "isPublished": true,
              "translations": {
                "en": { "name": "Backend" },
                "vi": { "name": "May chu" }
              }
            }
            """),
        _ when requestType == typeof(SkillWriteRequest) => Parse(
            """
            {
              "categoryId": "d1e6b31e-5907-4db9-874a-c0bdd13e74f4",
              "level": "Primary",
              "yearsOfExperience": 3.5,
              "icon": { "type": "lucide", "value": "database-zap" },
              "displayOrder": 0,
              "isPublished": true,
              "translations": {
                "en": { "name": "PostgreSQL" },
                "vi": { "name": "PostgreSQL" }
              }
            }
            """),
        _ when requestType == typeof(SkillReorderRequest) => ReorderExample(),
        _ when requestType == typeof(ExperienceWriteRequest) => Parse(
            """
            {
              "companyName": "Example Co",
              "employmentType": "Full-time",
              "companyUrl": "https://example.com",
              "startDate": "2024-01-01",
              "endDate": null,
              "displayOrder": 0,
              "isPublished": true,
              "translations": {
                "en": {
                  "position": "Backend Engineer",
                  "location": "Remote",
                  "description": "Built reliable web APIs."
                },
                "vi": {
                  "position": "Ky su Backend",
                  "location": "Tu xa",
                  "description": "Xay dung API web on dinh."
                }
              },
              "highlights": [
                { "locale": "en", "highlightType": "achievement", "content": "Reduced latency.", "displayOrder": 0 },
                { "locale": "vi", "highlightType": "achievement", "content": "Giam do tre.", "displayOrder": 0 }
              ],
              "technologyIds": ["9d5ba420-715c-43b5-a147-b98a90b05160"]
            }
            """),
        _ when requestType == typeof(ExperienceReorderRequest) => ReorderExample(),
        _ when requestType == typeof(ProjectWriteRequest) => Parse(
            """
            {
              "slug": "portfolio-api",
              "internalName": "Portfolio API",
              "kind": "personal",
              "disclosureLevel": "full",
              "repositoryUrl": "https://github.com/example/portfolio-api",
              "demoUrl": "https://portfolio.example.com",
              "thumbnailImageId": null,
              "startDate": "2026-01-01",
              "endDate": null,
              "isFeatured": true,
              "displayOrder": 0,
              "isPublished": true,
              "translations": {
                "en": {
                  "name": "Portfolio API",
                  "shortDescription": "A multilingual portfolio backend.",
                  "clientContext": null,
                  "role": "Backend Engineer",
                  "problem": "Portfolio content needed a secure API.",
                  "solution": "Built a layered ASP.NET Core API.",
                  "result": "Content can be managed and published safely."
                },
                "vi": {
                  "name": "API Portfolio",
                  "shortDescription": "Backend portfolio da ngon ngu.",
                  "clientContext": null,
                  "role": "Ky su Backend",
                  "problem": "Noi dung portfolio can mot API an toan.",
                  "solution": "Xay dung API ASP.NET Core phan lop.",
                  "result": "Noi dung duoc quan ly va xuat ban an toan."
                }
              },
              "technologyIds": ["9d5ba420-715c-43b5-a147-b98a90b05160"],
              "highlights": [
                { "locale": "en", "content": "Added private document storage.", "displayOrder": 0 },
                { "locale": "vi", "content": "Bo sung luu tru tai lieu rieng tu.", "displayOrder": 0 }
              ],
              "images": []
            }
            """),
        _ when requestType == typeof(ProjectPublishRequest) => Parse(
            """
            { "isPublished": true }
            """),
        _ when requestType == typeof(ProjectReorderRequest) => ReorderExample(),
        _ when requestType == typeof(CertificateWriteRequest) => Parse(
            """
            {
              "issuer": "Example Authority",
              "issuedDate": "2026-01-01",
              "expirationDate": null,
              "credentialId": "ABC-123",
              "showCredentialId": false,
              "credentialUrl": "https://example.com/verify/ABC-123",
              "displayOrder": 0,
              "isPublished": true,
              "translations": {
                "en": { "name": "Cloud Certificate" },
                "vi": { "name": "Chung chi Cloud" }
              },
              "technologyIds": ["9d5ba420-715c-43b5-a147-b98a90b05160"]
            }
            """),
        _ when requestType == typeof(ResumeCurrentRequest) => Parse(
            """
            { "isCurrent": true }
            """),
        _ when requestType == typeof(ResumePublishRequest) => Parse(
            """
            { "isPublished": true }
            """),
        _ => null,
    };

    private static JsonNode ReorderExample() => Parse(
        """
        {
          "items": [
            { "id": "4e274d29-a8eb-4b9e-8ad1-5391ab2f0932", "displayOrder": 0 },
            { "id": "4d0e90ac-d424-45a4-84ef-898587398ac7", "displayOrder": 1 }
          ]
        }
        """);

    private static JsonNode Parse(string json) => JsonNode.Parse(json)!;
}
