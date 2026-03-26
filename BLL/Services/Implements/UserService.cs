using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements
{
    public class UserService : BaseService<User>, IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository) : base(userRepository)
        {
            _userRepository = userRepository;
        }

        public override async Task<(IEnumerable<User> Items, int TotalCount)> GetAllAsync(
            int page,
            int pageSize,
            string? sortBy = null,
            string? sortOrder = null,
            string? search = null,
            Dictionary<string, string>? filters = null,
            IEnumerable<string>? allowedColumns = null)
        {
            var effectiveAllowedColumns = new[]
            {
                "UserId",
                "Email",
                "Role",
                "FullName",
                "Phone",
                "IdentityVerified",
                "CreatedAt"
            };

            return await base.GetAllAsync(page, pageSize, sortBy, sortOrder, search, filters, effectiveAllowedColumns);
        }
    }
}
