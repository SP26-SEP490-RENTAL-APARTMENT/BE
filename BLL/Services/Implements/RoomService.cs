using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class RoomService(IRepository<Room> repository)
    : BaseService<Room>(repository), IRoomService
{
}
