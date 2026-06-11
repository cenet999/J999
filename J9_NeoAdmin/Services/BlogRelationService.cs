using FreeSql;
using LinCms.Entities.Blog;

namespace J9_NeoAdmin.Services;

/// <summary>
/// 博客多对多关联读写。
/// </summary>
public static class BlogRelationService
{
    public static async Task<IReadOnlyCollection<long>> GetChannelTagIdsAsync(IFreeSql freeSql, long channelId) =>
        await freeSql.Select<Tag2.ChannelTag2>()
            .Where(a => a.ChannelId == channelId)
            .ToListAsync(a => a.TagId);

    public static async Task SaveChannelTagsAsync(IFreeSql freeSql, long channelId, IReadOnlyCollection<long> tagIds)
    {
        await freeSql.Delete<Tag2.ChannelTag2>().Where(a => a.ChannelId == channelId).ExecuteAffrowsAsync();
        if (tagIds.Count == 0)
        {
            return;
        }

        await freeSql.Insert(tagIds.Select(tagId => new Tag2.ChannelTag2
        {
            ChannelId = channelId,
            TagId = tagId,
        })).ExecuteAffrowsAsync();
    }

    public static async Task<IReadOnlyCollection<long>> GetCollectionArticleIdsAsync(IFreeSql freeSql, long collectionId) =>
        await freeSql.Select<Article.ArticleCollection>()
            .Where(a => a.CollectionId == collectionId)
            .ToListAsync(a => a.ArticleId);

    public static async Task SaveCollectionArticlesAsync(
        IFreeSql freeSql,
        long collectionId,
        IReadOnlyCollection<long> articleIds,
        long? userId,
        string userName)
    {
        await freeSql.Delete<Article.ArticleCollection>()
            .Where(a => a.CollectionId == collectionId)
            .ExecuteAffrowsAsync();

        if (articleIds.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.Now;
        await freeSql.Insert(articleIds.Select(articleId => new Article.ArticleCollection
        {
            ArticleId = articleId,
            CollectionId = collectionId,
            CreatedUserId = userId,
            CreatedUserName = userName,
            CreatedTime = now,
        })).ExecuteAffrowsAsync();
    }

    public static async Task<IReadOnlyCollection<long>> GetArticleTagIdsAsync(IFreeSql freeSql, long articleId) =>
        await freeSql.Select<Tag2.TagArticle>()
            .Where(a => a.ArticleId == articleId)
            .ToListAsync(a => a.TagId);

    public static async Task SaveArticleTagsAsync(IFreeSql freeSql, long articleId, IReadOnlyCollection<long> tagIds)
    {
        await freeSql.Delete<Tag2.TagArticle>().Where(a => a.ArticleId == articleId).ExecuteAffrowsAsync();
        if (tagIds.Count == 0)
        {
            return;
        }

        await freeSql.Insert(tagIds.Select(tagId => new Tag2.TagArticle
        {
            ArticleId = articleId,
            TagId = tagId,
        })).ExecuteAffrowsAsync();
    }

    public static async Task<IReadOnlyCollection<long>> GetArticleCollectionIdsAsync(IFreeSql freeSql, long articleId) =>
        await freeSql.Select<Article.ArticleCollection>()
            .Where(a => a.ArticleId == articleId)
            .ToListAsync(a => a.CollectionId);

    public static async Task SaveArticleCollectionsAsync(
        IFreeSql freeSql,
        long articleId,
        IReadOnlyCollection<long> collectionIds,
        long? userId,
        string userName)
    {
        await freeSql.Delete<Article.ArticleCollection>()
            .Where(a => a.ArticleId == articleId)
            .ExecuteAffrowsAsync();

        if (collectionIds.Count == 0)
        {
            return;
        }

        DateTime now = DateTime.Now;
        await freeSql.Insert(collectionIds.Select(collectionId => new Article.ArticleCollection
        {
            ArticleId = articleId,
            CollectionId = collectionId,
            CreatedUserId = userId,
            CreatedUserName = userName,
            CreatedTime = now,
        })).ExecuteAffrowsAsync();
    }

    public static async Task<IReadOnlyCollection<long>> GetTagChannelIdsAsync(IFreeSql freeSql, long tagId) =>
        await freeSql.Select<Tag2.ChannelTag2>()
            .Where(a => a.TagId == tagId)
            .ToListAsync(a => a.ChannelId);

    public static async Task SaveTagChannelsAsync(IFreeSql freeSql, long tagId, IReadOnlyCollection<long> channelIds)
    {
        await freeSql.Delete<Tag2.ChannelTag2>().Where(a => a.TagId == tagId).ExecuteAffrowsAsync();
        if (channelIds.Count == 0)
        {
            return;
        }

        await freeSql.Insert(channelIds.Select(channelId => new Tag2.ChannelTag2
        {
            ChannelId = channelId,
            TagId = tagId,
        })).ExecuteAffrowsAsync();
    }

    public static async Task<List<Tag2>> LoadTagsByIdsAsync(IFreeSql freeSql, IReadOnlyCollection<long> tagIds)
    {
        if (tagIds.Count == 0)
        {
            return [];
        }

        return await freeSql.Select<Tag2>().Where(t => tagIds.Contains(t.Id)).ToListAsync();
    }

    public static async Task<List<Collection>> LoadCollectionsByIdsAsync(IFreeSql freeSql, IReadOnlyCollection<long> collectionIds)
    {
        if (collectionIds.Count == 0)
        {
            return [];
        }

        return await freeSql.Select<Collection>().Where(c => collectionIds.Contains(c.Id)).ToListAsync();
    }

    public static async Task<List<Channel>> LoadChannelsByIdsAsync(IFreeSql freeSql, IReadOnlyCollection<long> channelIds)
    {
        if (channelIds.Count == 0)
        {
            return [];
        }

        return await freeSql.Select<Channel>().Where(c => channelIds.Contains(c.Id)).ToListAsync();
    }
}
