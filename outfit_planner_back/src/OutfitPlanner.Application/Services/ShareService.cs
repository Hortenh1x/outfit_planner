using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Application.Common;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Application.Services;

public sealed class ShareService
{
    private readonly IOutfitRepository _outfits;
    private readonly IShareLinkRepository _shareLinks;
    private readonly IShareTokenGenerator _tokens;
    private readonly IClock _clock;

    public ShareService(
        IOutfitRepository outfits,
        IShareLinkRepository shareLinks,
        IShareTokenGenerator tokens,
        IClock clock)
    {
        _outfits = outfits;
        _shareLinks = shareLinks;
        _tokens = tokens;
        _clock = clock;
    }

    public ShareLink CreateShareLink(string userId, Guid outfitId)
    {
        var normalizedUserId = InputGuard.NormalizeUserId(userId);
        if (_outfits.GetOutfitByUser(normalizedUserId, outfitId) is null)
        {
            throw new ValidationException("Outfit was not found.");
        }

        var link = new ShareLink(_tokens.CreateToken(), normalizedUserId, outfitId, _clock.UtcNow, null);
        _shareLinks.AddShareLink(link);
        return link;
    }

    public Outfit? GetSharedOutfit(string token)
    {
        var link = _shareLinks.GetActiveShareLink(InputGuard.RequireText(token, "Share token"));
        return link is null ? null : _outfits.GetOutfitById(link.OutfitId);
    }

    public bool RevokeShareLink(string userId, string token)
    {
        return _shareLinks.RevokeShareLinkByUser(
            InputGuard.NormalizeUserId(userId),
            InputGuard.RequireText(token, "Share token"),
            _clock.UtcNow);
    }
}
