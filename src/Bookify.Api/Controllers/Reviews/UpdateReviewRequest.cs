using System.Text.Json.Serialization;

namespace Bookify.Api.Controllers.Reviews;

public sealed record UpdateReviewRequest([property: JsonRequired] int Rating, string Comment);
