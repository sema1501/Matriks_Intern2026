using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Services;

public class FeedbackService(AppDbContext db) : IFeedbackService
{
    public async Task<List<FeedbackDto>> GetAllAsync()
    {
        var feedbacks = await db.Feedbacks
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync();

        return feedbacks.Select(f => new FeedbackDto
        {
            Id = f.Id,
            Message = f.Message,
            Rating = f.Rating,
            CreatedAt = f.CreatedAt,
            UserId = f.UserId
        }).ToList();
    }

    public async Task CreateAsync(CreateFeedbackDto request, int? userId)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Mesaj boş olamaz.");
        }

        if (request.Rating.HasValue && (request.Rating < 1 || request.Rating > 5))
        {
            throw new ArgumentException("Puan 1 ile 5 arasında olmalıdır.");
        }

        var feedback = new Feedback
        {
            UserId = userId,
            Message = request.Message,
            Rating = request.Rating,
            CreatedAt = DateTime.Now
        };

        db.Feedbacks.Add(feedback);
        await db.SaveChangesAsync();
    }
}