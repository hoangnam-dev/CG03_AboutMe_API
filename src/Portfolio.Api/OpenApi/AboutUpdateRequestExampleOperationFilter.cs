using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Portfolio.Application.About;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Portfolio.Api.OpenApi;

public sealed class AboutUpdateRequestExampleOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var acceptsAboutUpdateRequest = context.MethodInfo
            .GetParameters()
            .Any(parameter => parameter.ParameterType == typeof(AboutUpdateRequest));

        if (!acceptsAboutUpdateRequest || operation.RequestBody?.Content is not { } content)
        {
            return;
        }

        foreach (var mediaType in content.Values)
        {
            mediaType.Example = new JsonObject
            {
                ["yearsOfExperience"] = 2,
                ["projectCount"] = 3,
                ["technologyCount"] = 15,
                ["showYearsOfExperience"] = true,
                ["showProjectCount"] = true,
                ["showTechnologyCount"] = true,
                ["showContactSection"] = true,
                ["isPublished"] = true,
                ["translations"] = new JsonObject
                {
                    ["en"] = new JsonObject
                    {
                        ["content"] = "Backend developer focused on reliable web APIs.",
                        ["careerGoal"] = "Build secure and maintainable software.",
                    },
                    ["vi"] = new JsonObject
                    {
                        ["content"] = "Backend developer building reliable web APIs.",
                        ["careerGoal"] = "Create secure and maintainable software.",
                    },
                },
            };
        }
    }
}
