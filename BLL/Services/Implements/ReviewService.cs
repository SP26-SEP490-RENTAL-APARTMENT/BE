using BLL.Services.Interfaces;
using DAL.Models;
using DAL.Repository.Interfaces;

namespace BLL.Services.Implements;

public sealed class ReviewService(IRepository<Review> repository)
    : BaseService<Review>(repository), IReviewService
{
}
