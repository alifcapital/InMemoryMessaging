using InMemoryMessaging.Managers;
using Microsoft.AspNetCore.Mvc;
using UsersService.Messaging.Events;
using UsersService.Models;

namespace UsersService.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController(IMessageManager messageManager) : ControllerBase
{
    private static readonly Dictionary<Guid, User> Items = new();

    [HttpGet]
    public IActionResult GetItems()
    {
        return Ok(Items.Values);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetItems(Guid id)
    {
        if (!Items.TryGetValue(id, out User item))
            return NotFound();

        return Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] User item)
    {
        Items.Add(item.Id, item);

        var userCreated = new UserCreated { UserId = item.Id, UserName = item.Name };
        await messageManager.PublishAsync(userCreated);

        return Ok();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromQuery] string newName)
    {
        if (!Items.TryGetValue(id, out var item))
            return NotFound();

        var userUpdated = new UserUpdated { UserId = item.Id, OldUserName = item.Name, NewUserName = newName };
        item.Name = newName;
        
        await messageManager.PublishAsync(userUpdated);

        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!Items.TryGetValue(id, out var item))
            return NotFound();

        var userDeleted = new UserDeleted { UserId = item.Id, UserName = item.Name };
        Items.Remove(id);
        await messageManager.PublishAsync(userDeleted);
        
        return Ok(item);
    }
}