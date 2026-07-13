using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

public interface IFeedbackService
{
    Task<List<FeedbackDto>> GetAllAsync();

    Task CreateAsync(CreateFeedbackDto request, int? userId);
}