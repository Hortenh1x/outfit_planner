using Npgsql;
using OutfitPlanner.Application.Abstractions;
using OutfitPlanner.Domain;

namespace OutfitPlanner.Infrastructure.Storage;

public sealed class PostgresOutfitStore :
    IBodyReferencePhotoRepository,
    IGarmentRepository,
    IOutfitRepository,
    IOutfitScheduleRepository,
    ITryOnJobRepository,
    IShareLinkRepository,
    IUserAccountRepository
{
    public static readonly IReadOnlyList<string> RequiredTables = new[]
    {
        "users",
        "auth_external_logins",
        "auth_sessions",
        "body_reference_photos",
        "garment_items",
        "outfits",
        "outfit_items",
        "scheduled_outfits",
        "try_on_jobs",
        "share_links"
    };

    private readonly NpgsqlDataSource _dataSource;

    public PostgresOutfitStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public void AddBodyReferencePhoto(BodyReferencePhoto photo)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, photo.UserId);

        using var command = new NpgsqlCommand("""
            insert into body_reference_photos (id, user_id, image_url, created_at)
            values (@id, @user_id, @image_url, @created_at)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", photo.Id);
        command.Parameters.AddWithValue("user_id", photo.UserId);
        command.Parameters.AddWithValue("image_url", photo.ImageUrl);
        command.Parameters.AddWithValue("created_at", photo.CreatedAt);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<BodyReferencePhoto> ListBodyReferencePhotosByUser(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, image_url, created_at
            from body_reference_photos
            where user_id = @user_id
            order by created_at desc
            """);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var photos = new List<BodyReferencePhoto>();
        while (reader.Read())
        {
            photos.Add(new BodyReferencePhoto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return photos;
    }

    public BodyReferencePhoto? GetBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, image_url, created_at
            from body_reference_photos
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", photoId);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new BodyReferencePhoto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3))
            : null;
    }

    public bool DeleteBodyReferencePhotoByUser(string userId, Guid photoId)
    {
        using var command = _dataSource.CreateCommand("""
            delete from body_reference_photos
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", photoId);
        return command.ExecuteNonQuery() > 0;
    }

    public void AddGarment(GarmentItem garment)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, garment.UserId);

        using var command = new NpgsqlCommand("""
            insert into garment_items (id, user_id, name, category, body_zone, image_url, thumbnail_url, tags, created_at)
            values (@id, @user_id, @name, @category, @body_zone, @image_url, @thumbnail_url, @tags, @created_at)
            """, connection, transaction);
        AddGarmentParameters(command, garment);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public GarmentItem? GetGarmentByUser(string userId, Guid garmentId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, name, category, body_zone, image_url, thumbnail_url, tags, created_at
            from garment_items
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", garmentId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadGarment(reader) : null;
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, name, category, body_zone, image_url, thumbnail_url, tags, created_at
            from garment_items
            where user_id = @user_id
            order by category, name
            """);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var garments = new List<GarmentItem>();
        while (reader.Read())
        {
            garments.Add(ReadGarment(reader));
        }

        return garments;
    }

    public bool DeleteGarmentByUser(string userId, Guid garmentId)
    {
        using var command = _dataSource.CreateCommand("""
            delete from garment_items
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", garmentId);
        return command.ExecuteNonQuery() > 0;
    }

    public void AddOutfit(Outfit outfit)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, outfit.UserId);

        using var outfitCommand = new NpgsqlCommand("""
            insert into outfits (id, user_id, name, clothes_only_preview_url, person_preview_url, created_at)
            values (@id, @user_id, @name, @clothes_only_preview_url, @person_preview_url, @created_at)
            """, connection, transaction);
        AddOutfitParameters(outfitCommand, outfit);
        outfitCommand.ExecuteNonQuery();

        foreach (var item in outfit.Items)
        {
            using var itemCommand = new NpgsqlCommand("""
                insert into outfit_items (outfit_id, garment_id, category)
                values (@outfit_id, @garment_id, @category)
                """, connection, transaction);
            itemCommand.Parameters.AddWithValue("outfit_id", outfit.Id);
            itemCommand.Parameters.AddWithValue("garment_id", item.GarmentId);
            itemCommand.Parameters.AddWithValue("category", item.Category.ToString());
            itemCommand.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public Outfit? GetOutfitByUser(string userId, Guid outfitId)
    {
        return GetOutfit("where user_id = @user_id and id = @id", command =>
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("id", outfitId);
        });
    }

    public Outfit? GetOutfitById(Guid outfitId)
    {
        return GetOutfit("where id = @id", command => command.Parameters.AddWithValue("id", outfitId));
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, name, clothes_only_preview_url, person_preview_url, created_at
            from outfits
            where user_id = @user_id
            order by created_at desc
            """);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var outfits = new List<Outfit>();
        while (reader.Read())
        {
            outfits.Add(ReadOutfitShell(reader));
        }

        return outfits.Select(outfit => outfit with { Items = ListOutfitItems(outfit.Id) }).ToList();
    }

    public void UpdateOutfit(Outfit outfit)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var outfitCommand = new NpgsqlCommand("""
            update outfits
            set name = @name,
                clothes_only_preview_url = @clothes_only_preview_url,
                person_preview_url = @person_preview_url
            where id = @id and user_id = @user_id
            """, connection, transaction);
        outfitCommand.Parameters.AddWithValue("id", outfit.Id);
        outfitCommand.Parameters.AddWithValue("user_id", outfit.UserId);
        outfitCommand.Parameters.AddWithValue("name", outfit.Name);
        outfitCommand.Parameters.AddWithValue("clothes_only_preview_url", DbValue(outfit.ClothesOnlyPreviewUrl));
        outfitCommand.Parameters.AddWithValue("person_preview_url", DbValue(outfit.PersonPreviewUrl));
        outfitCommand.ExecuteNonQuery();

        transaction.Commit();
    }

    public void UpsertScheduledOutfit(ScheduledOutfit scheduled)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, scheduled.UserId);

        using var command = new NpgsqlCommand("""
            insert into scheduled_outfits (id, user_id, date, outfit_id, created_at)
            values (@id, @user_id, @date, @outfit_id, @created_at)
            on conflict (user_id, date) do update
            set id = excluded.id,
                outfit_id = excluded.outfit_id,
                created_at = excluded.created_at
            """, connection, transaction);
        command.Parameters.AddWithValue("id", scheduled.Id);
        command.Parameters.AddWithValue("user_id", scheduled.UserId);
        command.Parameters.AddWithValue("date", scheduled.Date);
        command.Parameters.AddWithValue("outfit_id", scheduled.OutfitId);
        command.Parameters.AddWithValue("created_at", scheduled.CreatedAt);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<ScheduledOutfit> ListScheduleByUser(string userId, DateOnly from, DateOnly to)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, date, outfit_id, created_at
            from scheduled_outfits
            where user_id = @user_id and date >= @from and date <= @to
            order by date
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("from", from);
        command.Parameters.AddWithValue("to", to);

        using var reader = command.ExecuteReader();
        var scheduled = new List<ScheduledOutfit>();
        while (reader.Read())
        {
            scheduled.Add(new ScheduledOutfit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetFieldValue<DateOnly>(2),
                reader.GetGuid(3),
                reader.GetFieldValue<DateTimeOffset>(4)));
        }

        return scheduled;
    }

    public void AddTryOnJob(TryOnJob job)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, job.UserId);

        using var command = new NpgsqlCommand("""
            insert into try_on_jobs (id, user_id, outfit_id, body_reference_photo_url, status, provider_job_id, output_image_url, error, created_at, updated_at)
            values (@id, @user_id, @outfit_id, @body_reference_photo_url, @status, @provider_job_id, @output_image_url, @error, @created_at, @updated_at)
            """, connection, transaction);
        AddTryOnJobParameters(command, job);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public TryOnJob? GetTryOnJobByUser(string userId, Guid jobId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, outfit_id, body_reference_photo_url, status, provider_job_id, output_image_url, error, created_at, updated_at
            from try_on_jobs
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", jobId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTryOnJob(reader) : null;
    }

    public void UpdateTryOnJob(TryOnJob job)
    {
        using var command = _dataSource.CreateCommand("""
            update try_on_jobs
            set status = @status,
                provider_job_id = @provider_job_id,
                output_image_url = @output_image_url,
                error = @error,
                updated_at = @updated_at
            where id = @id and user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("user_id", job.UserId);
        command.Parameters.AddWithValue("status", job.Status.ToString());
        command.Parameters.AddWithValue("provider_job_id", DbValue(job.ProviderJobId));
        command.Parameters.AddWithValue("output_image_url", DbValue(job.OutputImageUrl));
        command.Parameters.AddWithValue("error", DbValue(job.Error));
        command.Parameters.AddWithValue("updated_at", job.UpdatedAt);
        command.ExecuteNonQuery();
    }

    public void AddShareLink(ShareLink link)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, link.UserId);

        using var command = new NpgsqlCommand("""
            insert into share_links (token, user_id, outfit_id, created_at, revoked_at)
            values (@token, @user_id, @outfit_id, @created_at, @revoked_at)
            """, connection, transaction);
        command.Parameters.AddWithValue("token", link.Token);
        command.Parameters.AddWithValue("user_id", link.UserId);
        command.Parameters.AddWithValue("outfit_id", link.OutfitId);
        command.Parameters.AddWithValue("created_at", link.CreatedAt);
        command.Parameters.AddWithValue("revoked_at", DbValue(link.RevokedAt));
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public ShareLink? GetActiveShareLink(string token)
    {
        using var command = _dataSource.CreateCommand("""
            select token, user_id, outfit_id, created_at, revoked_at
            from share_links
            where token = @token and revoked_at is null
            """);
        command.Parameters.AddWithValue("token", token);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ShareLink(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetGuid(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    public void AddUser(UserAccount user)
    {
        using var command = _dataSource.CreateCommand("""
            insert into users (id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at)
            values (@id, @email, @normalized_email, @display_name, @password_hash, @created_at, @updated_at, @last_login_at)
            """);
        AddUserParameters(command, user);
        command.ExecuteNonQuery();
    }

    public void UpdateUser(UserAccount user)
    {
        using var command = _dataSource.CreateCommand("""
            update users
            set email = @email,
                normalized_email = @normalized_email,
                display_name = @display_name,
                password_hash = @password_hash,
                updated_at = @updated_at,
                last_login_at = @last_login_at
            where id = @id
            """);
        AddUserParameters(command, user);
        command.ExecuteNonQuery();
    }

    public UserAccount? GetUserById(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at
            from users
            where id = @id
            """);
        command.Parameters.AddWithValue("id", userId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public UserAccount? GetUserByNormalizedEmail(string normalizedEmail)
    {
        using var command = _dataSource.CreateCommand("""
            select id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at
            from users
            where normalized_email = @normalized_email
            """);
        command.Parameters.AddWithValue("normalized_email", normalizedEmail);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadUser(reader) : null;
    }

    public void AddExternalLogin(ExternalAuthLogin login)
    {
        using var command = _dataSource.CreateCommand("""
            insert into auth_external_logins (provider, provider_subject, user_id, email, created_at, last_login_at)
            values (@provider, @provider_subject, @user_id, @email, @created_at, @last_login_at)
            """);
        AddExternalLoginParameters(command, login);
        command.ExecuteNonQuery();
    }

    public ExternalAuthLogin? GetExternalLogin(string provider, string providerSubject)
    {
        using var command = _dataSource.CreateCommand("""
            select provider, provider_subject, user_id, email, created_at, last_login_at
            from auth_external_logins
            where provider = @provider and provider_subject = @provider_subject
            """);
        command.Parameters.AddWithValue("provider", provider.ToLowerInvariant());
        command.Parameters.AddWithValue("provider_subject", providerSubject);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadExternalLogin(reader) : null;
    }

    public void UpdateExternalLogin(ExternalAuthLogin login)
    {
        using var command = _dataSource.CreateCommand("""
            update auth_external_logins
            set email = @email,
                last_login_at = @last_login_at
            where provider = @provider and provider_subject = @provider_subject
            """);
        AddExternalLoginParameters(command, login);
        command.ExecuteNonQuery();
    }

    public void AddAuthSession(AuthSession session)
    {
        using var command = _dataSource.CreateCommand("""
            insert into auth_sessions (id, user_id, token_hash, csrf_token_hash, expires_at, created_at, revoked_at)
            values (@id, @user_id, @token_hash, @csrf_token_hash, @expires_at, @created_at, @revoked_at)
            """);
        command.Parameters.AddWithValue("id", session.Id);
        command.Parameters.AddWithValue("user_id", session.UserId);
        command.Parameters.AddWithValue("token_hash", session.TokenHash);
        command.Parameters.AddWithValue("csrf_token_hash", session.CsrfTokenHash);
        command.Parameters.AddWithValue("expires_at", session.ExpiresAt);
        command.Parameters.AddWithValue("created_at", session.CreatedAt);
        command.Parameters.AddWithValue("revoked_at", DbValue(session.RevokedAt));
        command.ExecuteNonQuery();
    }

    public AuthSession? GetActiveAuthSessionByTokenHash(string tokenHash, DateTimeOffset now)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, token_hash, csrf_token_hash, expires_at, created_at, revoked_at
            from auth_sessions
            where token_hash = @token_hash
                and revoked_at is null
                and expires_at > @now
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("now", now);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadAuthSession(reader) : null;
    }

    public void RevokeAuthSessionByTokenHash(string tokenHash, DateTimeOffset revokedAt)
    {
        using var command = _dataSource.CreateCommand("""
            update auth_sessions
            set revoked_at = @revoked_at
            where token_hash = @token_hash and revoked_at is null
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("revoked_at", revokedAt);
        command.ExecuteNonQuery();
    }

    private Outfit? GetOutfit(string whereClause, Action<NpgsqlCommand> addParameters)
    {
        using var command = _dataSource.CreateCommand($"""
            select id, user_id, name, clothes_only_preview_url, person_preview_url, created_at
            from outfits
            {whereClause}
            """);
        addParameters(command);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var outfit = ReadOutfitShell(reader);
        return outfit with { Items = ListOutfitItems(outfit.Id) };
    }

    private IReadOnlyList<OutfitItem> ListOutfitItems(Guid outfitId)
    {
        using var command = _dataSource.CreateCommand("""
            select g.id, g.name, g.category, g.body_zone, g.thumbnail_url
            from outfit_items oi
            join garment_items g on g.id = oi.garment_id
            where oi.outfit_id = @outfit_id
            order by case oi.category when 'Top' then 1 when 'Bottom' then 2 else 3 end
            """);
        command.Parameters.AddWithValue("outfit_id", outfitId);

        using var reader = command.ExecuteReader();
        var items = new List<OutfitItem>();
        while (reader.Read())
        {
            items.Add(new OutfitItem(
                reader.GetGuid(0),
                reader.GetString(1),
                Enum.Parse<GarmentCategory>(reader.GetString(2)),
                Enum.Parse<BodyZone>(reader.GetString(3)),
                reader.GetString(4)));
        }

        return items;
    }

    private static void AddUserParameters(NpgsqlCommand command, UserAccount user)
    {
        command.Parameters.AddWithValue("id", user.Id);
        command.Parameters.AddWithValue("email", DbValue(user.Email));
        command.Parameters.AddWithValue("normalized_email", DbValue(user.NormalizedEmail));
        command.Parameters.AddWithValue("display_name", user.DisplayName);
        command.Parameters.AddWithValue("password_hash", DbValue(user.PasswordHash));
        command.Parameters.AddWithValue("created_at", user.CreatedAt);
        command.Parameters.AddWithValue("updated_at", user.UpdatedAt);
        command.Parameters.AddWithValue("last_login_at", DbValue(user.LastLoginAt));
    }

    private static void AddExternalLoginParameters(NpgsqlCommand command, ExternalAuthLogin login)
    {
        command.Parameters.AddWithValue("provider", login.Provider.ToLowerInvariant());
        command.Parameters.AddWithValue("provider_subject", login.ProviderSubject);
        command.Parameters.AddWithValue("user_id", login.UserId);
        command.Parameters.AddWithValue("email", DbValue(login.Email));
        command.Parameters.AddWithValue("created_at", login.CreatedAt);
        command.Parameters.AddWithValue("last_login_at", login.LastLoginAt);
    }

    private static UserAccount ReadUser(NpgsqlDataReader reader)
    {
        return new UserAccount(
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetFieldValue<DateTimeOffset>(6),
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }

    private static ExternalAuthLogin ReadExternalLogin(NpgsqlDataReader reader)
    {
        return new ExternalAuthLogin(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static AuthSession ReadAuthSession(NpgsqlDataReader reader)
    {
        return new AuthSession(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.GetFieldValue<DateTimeOffset>(5),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6));
    }

    private static void EnsureUser(NpgsqlConnection connection, NpgsqlTransaction transaction, string userId)
    {
        using var command = new NpgsqlCommand("""
            insert into users (id, display_name, created_at, updated_at)
            values (@id, @display_name, now(), now())
            on conflict (id) do nothing
            """, connection, transaction);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("display_name", userId);
        command.ExecuteNonQuery();
    }

    private static void AddGarmentParameters(NpgsqlCommand command, GarmentItem garment)
    {
        command.Parameters.AddWithValue("id", garment.Id);
        command.Parameters.AddWithValue("user_id", garment.UserId);
        command.Parameters.AddWithValue("name", garment.Name);
        command.Parameters.AddWithValue("category", garment.Category.ToString());
        command.Parameters.AddWithValue("body_zone", garment.BodyZone.ToString());
        command.Parameters.AddWithValue("image_url", garment.ImageUrl);
        command.Parameters.AddWithValue("thumbnail_url", garment.ThumbnailUrl);
        command.Parameters.AddWithValue("tags", garment.Tags.ToArray());
        command.Parameters.AddWithValue("created_at", garment.CreatedAt);
    }

    private static void AddOutfitParameters(NpgsqlCommand command, Outfit outfit)
    {
        command.Parameters.AddWithValue("id", outfit.Id);
        command.Parameters.AddWithValue("user_id", outfit.UserId);
        command.Parameters.AddWithValue("name", outfit.Name);
        command.Parameters.AddWithValue("clothes_only_preview_url", DbValue(outfit.ClothesOnlyPreviewUrl));
        command.Parameters.AddWithValue("person_preview_url", DbValue(outfit.PersonPreviewUrl));
        command.Parameters.AddWithValue("created_at", outfit.CreatedAt);
    }

    private static void AddTryOnJobParameters(NpgsqlCommand command, TryOnJob job)
    {
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("user_id", job.UserId);
        command.Parameters.AddWithValue("outfit_id", job.OutfitId);
        command.Parameters.AddWithValue("body_reference_photo_url", job.BodyReferencePhotoUrl);
        command.Parameters.AddWithValue("status", job.Status.ToString());
        command.Parameters.AddWithValue("provider_job_id", DbValue(job.ProviderJobId));
        command.Parameters.AddWithValue("output_image_url", DbValue(job.OutputImageUrl));
        command.Parameters.AddWithValue("error", DbValue(job.Error));
        command.Parameters.AddWithValue("created_at", job.CreatedAt);
        command.Parameters.AddWithValue("updated_at", job.UpdatedAt);
    }

    private static GarmentItem ReadGarment(NpgsqlDataReader reader)
    {
        return new GarmentItem(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Enum.Parse<GarmentCategory>(reader.GetString(3)),
            Enum.Parse<BodyZone>(reader.GetString(4)),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetFieldValue<string[]>(7),
            reader.GetFieldValue<DateTimeOffset>(8));
    }

    private static Outfit ReadOutfitShell(NpgsqlDataReader reader)
    {
        return new Outfit(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Array.Empty<OutfitItem>(),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5));
    }

    private static TryOnJob ReadTryOnJob(NpgsqlDataReader reader)
    {
        return new TryOnJob(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetString(3),
            Enum.Parse<TryOnStatus>(reader.GetString(4)),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static object DbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}
