using MBTM.Api.Auth;
using MBTM.Api.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace MBTM.Api.Functions;

/// <summary>
/// タスクのCRUD API。すべてアクセストークン(スコープ access_as_user)が必須で、
/// 操作できるのはトークンのユーザー(oid)自身のタスクのみ。
/// </summary>
public class TasksFunction(TokenValidator auth, TaskRepository repository)
{
    private const string Route = "tasks";

    [Function("Tasks_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequest req,
        CancellationToken ct)
    {
        var result = await auth.ValidateAsync(req, ct);
        if (result.User is null) return new StatusCodeResult(result.StatusCode);

        var items = await repository.ListAsync(result.User.UserId, ct);
        return new OkObjectResult(items.Select(TaskDto.From));
    }

    [Function("Tasks_Get")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route + "/{id}")] HttpRequest req,
        string id,
        CancellationToken ct)
    {
        var result = await auth.ValidateAsync(req, ct);
        if (result.User is null) return new StatusCodeResult(result.StatusCode);

        var entity = await repository.GetAsync(result.User.UserId, id, ct);
        return entity is null ? new NotFoundResult() : new OkObjectResult(TaskDto.From(entity));
    }

    [Function("Tasks_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = Route)] HttpRequest req,
        CancellationToken ct)
    {
        var result = await auth.ValidateAsync(req, ct);
        if (result.User is null) return new StatusCodeResult(result.StatusCode);

        var input = await ReadInputAsync(req, ct);
        if (TaskValidation.Validate(input) is { } error) return new BadRequestObjectResult(new { error });

        var entity = await repository.CreateAsync(result.User.UserId, input!, ct);
        var dto = TaskDto.From(entity);
        return new CreatedResult($"{req.GetEncodedUrl().TrimEnd('/')}/{dto.Id}", dto);
    }

    [Function("Tasks_Update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = Route + "/{id}")] HttpRequest req,
        string id,
        CancellationToken ct)
    {
        var result = await auth.ValidateAsync(req, ct);
        if (result.User is null) return new StatusCodeResult(result.StatusCode);

        var input = await ReadInputAsync(req, ct);
        if (TaskValidation.Validate(input) is { } error) return new BadRequestObjectResult(new { error });

        var entity = await repository.UpdateAsync(result.User.UserId, id, input!, ct);
        return entity is null ? new NotFoundResult() : new OkObjectResult(TaskDto.From(entity));
    }

    [Function("Tasks_Delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = Route + "/{id}")] HttpRequest req,
        string id,
        CancellationToken ct)
    {
        var result = await auth.ValidateAsync(req, ct);
        if (result.User is null) return new StatusCodeResult(result.StatusCode);

        return await repository.DeleteAsync(result.User.UserId, id, ct)
            ? new NoContentResult()
            : new NotFoundResult();
    }

    private static async Task<TaskInput?> ReadInputAsync(HttpRequest req, CancellationToken ct)
    {
        try
        {
            return await req.ReadFromJsonAsync<TaskInput>(ct);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
