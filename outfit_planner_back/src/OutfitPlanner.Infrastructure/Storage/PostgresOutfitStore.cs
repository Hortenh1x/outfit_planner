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
            insert into garment_items (
                id, user_id, name, category, body_zone, image_url, thumbnail_url, tags,
                primary_color, secondary_colors, material, brand, size, season,
                weather_min_temp, weather_max_temp, occasion, formality_score, warmth_score,
                comfort_score, is_favorite, is_archived, last_worn_at, laundry_status, created_at)
            values (
                @id, @user_id, @name, @category, @body_zone, @image_url, @thumbnail_url, @tags,
                @primary_color, @secondary_colors, @material, @brand, @size, @season,
                @weather_min_temp, @weather_max_temp, @occasion, @formality_score, @warmth_score,
                @comfort_score, @is_favorite, @is_archived, @last_worn_at, @laundry_status, @created_at)
            """, connection, transaction);
        AddGarmentParameters(command, garment);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public GarmentItem? GetGarmentByUser(string userId, Guid garmentId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, name, category, body_zone, image_url, thumbnail_url, tags,
                primary_color, secondary_colors, material, brand, size, season,
                weather_min_temp, weather_max_temp, occasion, formality_score, warmth_score,
                comfort_score, is_favorite, is_archived, last_worn_at, laundry_status, created_at
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
        return ListGarmentsByUser(userId, new GarmentQuery());
    }

    public IReadOnlyList<GarmentItem> ListGarmentsByUser(string userId, GarmentQuery query)
    {
        var where = new List<string> { "user_id = @user_id" };
        using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("user_id", userId);

        AddGarmentQueryFilters(command, where, query);
        command.CommandText = $"""
            select id, user_id, name, category, body_zone, image_url, thumbnail_url, tags,
                primary_color, secondary_colors, material, brand, size, season,
                weather_min_temp, weather_max_temp, occasion, formality_score, warmth_score,
                comfort_score, is_favorite, is_archived, last_worn_at, laundry_status, created_at
            from garment_items
            where {string.Join(" and ", where)}
            {GarmentOrderBy(query.Sort)}
            {LimitOffsetClause(query.Limit, query.Offset)}
            """;
        AddLimitOffsetParameters(command, query.Limit, query.Offset);

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

    public void UpdateGarment(GarmentItem garment)
    {
        using var command = _dataSource.CreateCommand("""
            update garment_items
            set name = @name,
                category = @category,
                body_zone = @body_zone,
                tags = @tags,
                primary_color = @primary_color,
                secondary_colors = @secondary_colors,
                material = @material,
                brand = @brand,
                size = @size,
                season = @season,
                weather_min_temp = @weather_min_temp,
                weather_max_temp = @weather_max_temp,
                occasion = @occasion,
                formality_score = @formality_score,
                warmth_score = @warmth_score,
                comfort_score = @comfort_score,
                is_favorite = @is_favorite,
                is_archived = @is_archived,
                last_worn_at = @last_worn_at,
                laundry_status = @laundry_status
            where id = @id and user_id = @user_id
            """);
        AddGarmentParameters(command, garment);
        command.ExecuteNonQuery();
    }

    public void AddOutfit(Outfit outfit)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, outfit.UserId);

        using var outfitCommand = new NpgsqlCommand("""
            insert into outfits (id, user_id, name, tags, occasion, is_favorite, is_archived, clothes_only_preview_url, person_preview_url, created_at)
            values (@id, @user_id, @name, @tags, @occasion, @is_favorite, @is_archived, @clothes_only_preview_url, @person_preview_url, @created_at)
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
        return ListOutfitsByUser(userId, new OutfitQuery());
    }

    public IReadOnlyList<Outfit> ListOutfitsByUser(string userId, OutfitQuery query)
    {
        var where = new List<string> { "user_id = @user_id" };
        using var command = _dataSource.CreateCommand();
        command.Parameters.AddWithValue("user_id", userId);
        AddOutfitQueryFilters(command, where, query);
        command.CommandText = $"""
            select id, user_id, name, tags, occasion, is_favorite, is_archived, clothes_only_preview_url, person_preview_url, created_at
            from outfits
            where {string.Join(" and ", where)}
            {OutfitOrderBy(query.Sort)}
            {LimitOffsetClause(query.Limit, query.Offset)}
            """;
        AddLimitOffsetParameters(command, query.Limit, query.Offset);

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
                tags = @tags,
                occasion = @occasion,
                is_favorite = @is_favorite,
                is_archived = @is_archived,
                clothes_only_preview_url = @clothes_only_preview_url,
                person_preview_url = @person_preview_url
            where id = @id and user_id = @user_id
            """, connection, transaction);
        outfitCommand.Parameters.AddWithValue("id", outfit.Id);
        outfitCommand.Parameters.AddWithValue("user_id", outfit.UserId);
        outfitCommand.Parameters.AddWithValue("name", outfit.Name);
        outfitCommand.Parameters.AddWithValue("tags", outfit.Tags.ToArray());
        outfitCommand.Parameters.AddWithValue("occasion", outfit.Occasion.ToArray());
        outfitCommand.Parameters.AddWithValue("is_favorite", outfit.IsFavorite);
        outfitCommand.Parameters.AddWithValue("is_archived", outfit.IsArchived);
        outfitCommand.Parameters.AddWithValue("clothes_only_preview_url", DbValue(outfit.ClothesOnlyPreviewUrl));
        outfitCommand.Parameters.AddWithValue("person_preview_url", DbValue(outfit.PersonPreviewUrl));
        outfitCommand.ExecuteNonQuery();

        using var deleteItemsCommand = new NpgsqlCommand("""
            delete from outfit_items
            where outfit_id = @outfit_id
            """, connection, transaction);
        deleteItemsCommand.Parameters.AddWithValue("outfit_id", outfit.Id);
        deleteItemsCommand.ExecuteNonQuery();

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

    public bool DeleteOutfitByUser(string userId, Guid outfitId)
    {
        using var command = _dataSource.CreateCommand("""
            delete from outfits
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", outfitId);
        return command.ExecuteNonQuery() > 0;
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

    public bool DeleteScheduledOutfitByUserDate(string userId, DateOnly date)
    {
        using var command = _dataSource.CreateCommand("""
            delete from scheduled_outfits
            where user_id = @user_id and date = @date
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("date", date);
        return command.ExecuteNonQuery() > 0;
    }

    public void AddTryOnJob(TryOnJob job)
    {
        using var connection = _dataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();
        EnsureUser(connection, transaction, job.UserId);

        using var command = new NpgsqlCommand("""
            insert into try_on_jobs (
                id, user_id, outfit_id, body_reference_photo_url, sequential_flow_enabled, status,
                provider_job_id, output_image_url, error, created_at, updated_at,
                consent_accepted_at, provider_name, provider_request_id, source_body_photo_id, retention_until, is_deleted)
            values (
                @id, @user_id, @outfit_id, @body_reference_photo_url, @sequential_flow_enabled, @status,
                @provider_job_id, @output_image_url, @error, @created_at, @updated_at,
                @consent_accepted_at, @provider_name, @provider_request_id, @source_body_photo_id, @retention_until, @is_deleted)
            """, connection, transaction);
        AddTryOnJobParameters(command, job);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public TryOnJob? GetTryOnJobByUser(string userId, Guid jobId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, outfit_id, body_reference_photo_url, sequential_flow_enabled, status,
                provider_job_id, output_image_url, error, created_at, updated_at,
                consent_accepted_at, provider_name, provider_request_id, source_body_photo_id, retention_until, is_deleted
            from try_on_jobs
            where user_id = @user_id and id = @id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("id", jobId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTryOnJob(reader) : null;
    }

    public TryOnJob? GetTryOnJobById(Guid jobId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, outfit_id, body_reference_photo_url, sequential_flow_enabled, status,
                provider_job_id, output_image_url, error, created_at, updated_at,
                consent_accepted_at, provider_name, provider_request_id, source_body_photo_id, retention_until, is_deleted
            from try_on_jobs
            where id = @id
            """);
        command.Parameters.AddWithValue("id", jobId);

        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadTryOnJob(reader) : null;
    }

    public IReadOnlyList<TryOnJob> ListTryOnJobsByUser(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, outfit_id, body_reference_photo_url, sequential_flow_enabled, status,
                provider_job_id, output_image_url, error, created_at, updated_at,
                consent_accepted_at, provider_name, provider_request_id, source_body_photo_id, retention_until, is_deleted
            from try_on_jobs
            where user_id = @user_id
            order by created_at desc
            """);
        command.Parameters.AddWithValue("user_id", userId);

        using var reader = command.ExecuteReader();
        var jobs = new List<TryOnJob>();
        while (reader.Read())
        {
            jobs.Add(ReadTryOnJob(reader));
        }

        return jobs;
    }

    public void UpdateTryOnJob(TryOnJob job)
    {
        using var command = _dataSource.CreateCommand("""
            update try_on_jobs
            set status = @status,
                provider_job_id = @provider_job_id,
                provider_request_id = @provider_request_id,
                output_image_url = @output_image_url,
                error = @error,
                retention_until = @retention_until,
                is_deleted = @is_deleted,
                updated_at = @updated_at
            where id = @id and user_id = @user_id
            """);
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("user_id", job.UserId);
        command.Parameters.AddWithValue("status", job.Status.ToString());
        command.Parameters.AddWithValue("provider_job_id", DbValue(job.ProviderJobId));
        command.Parameters.AddWithValue("provider_request_id", DbValue(job.ProviderRequestId));
        command.Parameters.AddWithValue("output_image_url", DbValue(job.OutputImageUrl));
        command.Parameters.AddWithValue("error", DbValue(job.Error));
        command.Parameters.AddWithValue("retention_until", DbValue(job.RetentionUntil));
        command.Parameters.AddWithValue("is_deleted", job.IsDeleted);
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

    public bool RevokeShareLinkByUser(string userId, string token, DateTimeOffset revokedAt)
    {
        using var command = _dataSource.CreateCommand("""
            update share_links
            set revoked_at = @revoked_at
            where user_id = @user_id and token = @token and revoked_at is null
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("token", token);
        command.Parameters.AddWithValue("revoked_at", revokedAt);
        return command.ExecuteNonQuery() > 0;
    }

    public void AddUser(UserAccount user)
    {
        using var command = _dataSource.CreateCommand("""
            insert into users (id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at, email_verified_at, two_factor_enabled)
            values (@id, @email, @normalized_email, @display_name, @password_hash, @created_at, @updated_at, @last_login_at, @email_verified_at, @two_factor_enabled)
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
                last_login_at = @last_login_at,
                email_verified_at = @email_verified_at,
                two_factor_enabled = @two_factor_enabled
            where id = @id
            """);
        AddUserParameters(command, user);
        command.ExecuteNonQuery();
    }

    public UserAccount? GetUserById(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            select id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at, email_verified_at, two_factor_enabled
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
            select id, email, normalized_email, display_name, password_hash, created_at, updated_at, last_login_at, email_verified_at, two_factor_enabled
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

    public IReadOnlyList<AuthSession> ListAuthSessionsByUser(string userId, DateTimeOffset now)
    {
        using var command = _dataSource.CreateCommand("""
            select id, user_id, token_hash, csrf_token_hash, expires_at, created_at, revoked_at
            from auth_sessions
            where user_id = @user_id
                and revoked_at is null
                and expires_at > @now
            order by created_at desc
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("now", now);

        using var reader = command.ExecuteReader();
        var sessions = new List<AuthSession>();
        while (reader.Read())
        {
            sessions.Add(ReadAuthSession(reader));
        }

        return sessions;
    }

    public void RevokeAuthSessionsByUser(string userId, DateTimeOffset revokedAt)
    {
        using var command = _dataSource.CreateCommand("""
            update auth_sessions
            set revoked_at = @revoked_at
            where user_id = @user_id and revoked_at is null
            """);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("revoked_at", revokedAt);
        command.ExecuteNonQuery();
    }

    public int DeleteExpiredAuthSessions(DateTimeOffset now)
    {
        using var command = _dataSource.CreateCommand("""
            delete from auth_sessions
            where expires_at <= @now
            """);
        command.Parameters.AddWithValue("now", now);
        return command.ExecuteNonQuery();
    }

    public void AddEmailVerificationToken(AuthEmailVerificationToken token)
    {
        using var command = _dataSource.CreateCommand("""
            insert into auth_email_verification_tokens (token_hash, user_id, expires_at, created_at, used_at)
            values (@token_hash, @user_id, @expires_at, @created_at, @used_at)
            """);
        command.Parameters.AddWithValue("token_hash", token.TokenHash);
        command.Parameters.AddWithValue("user_id", token.UserId);
        command.Parameters.AddWithValue("expires_at", token.ExpiresAt);
        command.Parameters.AddWithValue("created_at", token.CreatedAt);
        command.Parameters.AddWithValue("used_at", DbValue(token.UsedAt));
        command.ExecuteNonQuery();
    }

    public AuthEmailVerificationToken? GetActiveEmailVerificationToken(string tokenHash, DateTimeOffset now)
    {
        using var command = _dataSource.CreateCommand("""
            select token_hash, user_id, expires_at, created_at, used_at
            from auth_email_verification_tokens
            where token_hash = @token_hash
                and used_at is null
                and expires_at > @now
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("now", now);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AuthEmailVerificationToken(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    public void MarkEmailVerificationTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        using var command = _dataSource.CreateCommand("""
            update auth_email_verification_tokens
            set used_at = @used_at
            where token_hash = @token_hash and used_at is null
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("used_at", usedAt);
        command.ExecuteNonQuery();
    }

    public void AddPasswordResetToken(AuthPasswordResetToken token)
    {
        using var command = _dataSource.CreateCommand("""
            insert into auth_password_reset_tokens (token_hash, user_id, expires_at, created_at, used_at)
            values (@token_hash, @user_id, @expires_at, @created_at, @used_at)
            """);
        command.Parameters.AddWithValue("token_hash", token.TokenHash);
        command.Parameters.AddWithValue("user_id", token.UserId);
        command.Parameters.AddWithValue("expires_at", token.ExpiresAt);
        command.Parameters.AddWithValue("created_at", token.CreatedAt);
        command.Parameters.AddWithValue("used_at", DbValue(token.UsedAt));
        command.ExecuteNonQuery();
    }

    public AuthPasswordResetToken? GetActivePasswordResetToken(string tokenHash, DateTimeOffset now)
    {
        using var command = _dataSource.CreateCommand("""
            select token_hash, user_id, expires_at, created_at, used_at
            from auth_password_reset_tokens
            where token_hash = @token_hash
                and used_at is null
                and expires_at > @now
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("now", now);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new AuthPasswordResetToken(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4))
            : null;
    }

    public void MarkPasswordResetTokenUsed(string tokenHash, DateTimeOffset usedAt)
    {
        using var command = _dataSource.CreateCommand("""
            update auth_password_reset_tokens
            set used_at = @used_at
            where token_hash = @token_hash and used_at is null
            """);
        command.Parameters.AddWithValue("token_hash", tokenHash);
        command.Parameters.AddWithValue("used_at", usedAt);
        command.ExecuteNonQuery();
    }

    public bool DeleteUserById(string userId)
    {
        using var command = _dataSource.CreateCommand("""
            delete from users
            where id = @user_id
            """);
        command.Parameters.AddWithValue("user_id", userId);
        return command.ExecuteNonQuery() > 0;
    }

    private static void AddGarmentQueryFilters(NpgsqlCommand command, List<string> where, GarmentQuery query)
    {
        if (query.Category is not null)
        {
            where.Add("category = @category");
            command.Parameters.AddWithValue("category", query.Category.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(query.Color))
        {
            where.Add("(primary_color = @color or secondary_colors @> @color_values)");
            command.Parameters.AddWithValue("color", query.Color);
            command.Parameters.AddWithValue("color_values", new[] { query.Color });
        }

        if (!string.IsNullOrWhiteSpace(query.Season))
        {
            where.Add("season @> @season_values");
            command.Parameters.AddWithValue("season_values", new[] { query.Season });
        }

        if (!string.IsNullOrWhiteSpace(query.Occasion))
        {
            where.Add("occasion @> @occasion_values");
            command.Parameters.AddWithValue("occasion_values", new[] { query.Occasion });
        }

        if (query.Favorite is not null)
        {
            where.Add("is_favorite = @is_favorite");
            command.Parameters.AddWithValue("is_favorite", query.Favorite.Value);
        }

        if (query.Archived is not null)
        {
            where.Add("is_archived = @is_archived");
            command.Parameters.AddWithValue("is_archived", query.Archived.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Brand))
        {
            where.Add("brand ilike @brand");
            command.Parameters.AddWithValue("brand", LikePattern(query.Brand));
        }

        if (!string.IsNullOrWhiteSpace(query.Material))
        {
            where.Add("material ilike @material");
            command.Parameters.AddWithValue("material", LikePattern(query.Material));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (name ilike @search
                    or primary_color ilike @search
                    or material ilike @search
                    or brand ilike @search
                    or size ilike @search
                    or exists (select 1 from unnest(tags) as tag where tag ilike @search))
                """);
            command.Parameters.AddWithValue("search", LikePattern(query.Search));
        }
    }

    private static void AddOutfitQueryFilters(NpgsqlCommand command, List<string> where, OutfitQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Occasion))
        {
            where.Add("occasion @> @occasion_values");
            command.Parameters.AddWithValue("occasion_values", new[] { query.Occasion });
        }

        if (query.Favorite is not null)
        {
            where.Add("is_favorite = @is_favorite");
            command.Parameters.AddWithValue("is_favorite", query.Favorite.Value);
        }

        if (query.Archived is not null)
        {
            where.Add("is_archived = @is_archived");
            command.Parameters.AddWithValue("is_archived", query.Archived.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (name ilike @search
                    or exists (select 1 from unnest(tags) as tag where tag ilike @search)
                    or exists (select 1 from unnest(occasion) as occasion_item where occasion_item ilike @search))
                """);
            command.Parameters.AddWithValue("search", LikePattern(query.Search));
        }
    }

    private static string GarmentOrderBy(string? sort)
    {
        return sort switch
        {
            "recent" => "order by created_at desc",
            "oldest" => "order by created_at asc",
            "name" => "order by name asc",
            _ => "order by category asc, name asc"
        };
    }

    private static string OutfitOrderBy(string? sort)
    {
        return sort switch
        {
            "oldest" => "order by created_at asc",
            "name" => "order by name asc",
            _ => "order by created_at desc"
        };
    }

    private static string LimitOffsetClause(int? limit, int? offset)
    {
        var clauses = new List<string>();
        if (limit is not null)
        {
            clauses.Add("limit @limit");
        }

        if (offset is not null)
        {
            clauses.Add("offset @offset");
        }

        return string.Join(Environment.NewLine, clauses);
    }

    private static void AddLimitOffsetParameters(NpgsqlCommand command, int? limit, int? offset)
    {
        if (limit is not null)
        {
            command.Parameters.AddWithValue("limit", limit.Value);
        }

        if (offset is not null)
        {
            command.Parameters.AddWithValue("offset", offset.Value);
        }
    }

    private static string LikePattern(string value)
    {
        return $"%{value}%";
    }

    private Outfit? GetOutfit(string whereClause, Action<NpgsqlCommand> addParameters)
    {
        using var command = _dataSource.CreateCommand($"""
            select id, user_id, name, tags, occasion, is_favorite, is_archived, clothes_only_preview_url, person_preview_url, created_at
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
        command.Parameters.AddWithValue("email_verified_at", DbValue(user.EmailVerifiedAt));
        command.Parameters.AddWithValue("two_factor_enabled", user.TwoFactorEnabled);
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
            reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7))
        {
            EmailVerifiedAt = reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
            TwoFactorEnabled = reader.GetBoolean(9)
        };
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
        command.Parameters.AddWithValue("primary_color", DbValue(garment.PrimaryColor));
        command.Parameters.AddWithValue("secondary_colors", garment.SecondaryColors.ToArray());
        command.Parameters.AddWithValue("material", DbValue(garment.Material));
        command.Parameters.AddWithValue("brand", DbValue(garment.Brand));
        command.Parameters.AddWithValue("size", DbValue(garment.Size));
        command.Parameters.AddWithValue("season", garment.Season.ToArray());
        command.Parameters.AddWithValue("weather_min_temp", DbValue(garment.WeatherMinTemp));
        command.Parameters.AddWithValue("weather_max_temp", DbValue(garment.WeatherMaxTemp));
        command.Parameters.AddWithValue("occasion", garment.Occasion.ToArray());
        command.Parameters.AddWithValue("formality_score", DbValue(garment.FormalityScore));
        command.Parameters.AddWithValue("warmth_score", DbValue(garment.WarmthScore));
        command.Parameters.AddWithValue("comfort_score", DbValue(garment.ComfortScore));
        command.Parameters.AddWithValue("is_favorite", garment.IsFavorite);
        command.Parameters.AddWithValue("is_archived", garment.IsArchived);
        command.Parameters.AddWithValue("last_worn_at", DbValue(garment.LastWornAt));
        command.Parameters.AddWithValue("laundry_status", garment.LaundryStatus);
        command.Parameters.AddWithValue("created_at", garment.CreatedAt);
    }

    private static void AddOutfitParameters(NpgsqlCommand command, Outfit outfit)
    {
        command.Parameters.AddWithValue("id", outfit.Id);
        command.Parameters.AddWithValue("user_id", outfit.UserId);
        command.Parameters.AddWithValue("name", outfit.Name);
        command.Parameters.AddWithValue("tags", outfit.Tags.ToArray());
        command.Parameters.AddWithValue("occasion", outfit.Occasion.ToArray());
        command.Parameters.AddWithValue("is_favorite", outfit.IsFavorite);
        command.Parameters.AddWithValue("is_archived", outfit.IsArchived);
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
        command.Parameters.AddWithValue("sequential_flow_enabled", job.SequentialFlowEnabled);
        command.Parameters.AddWithValue("status", job.Status.ToString());
        command.Parameters.AddWithValue("provider_job_id", DbValue(job.ProviderJobId));
        command.Parameters.AddWithValue("output_image_url", DbValue(job.OutputImageUrl));
        command.Parameters.AddWithValue("error", DbValue(job.Error));
        command.Parameters.AddWithValue("created_at", job.CreatedAt);
        command.Parameters.AddWithValue("updated_at", job.UpdatedAt);
        command.Parameters.AddWithValue("consent_accepted_at", DbValue(job.ConsentAcceptedAt));
        command.Parameters.AddWithValue("provider_name", DbValue(job.ProviderName));
        command.Parameters.AddWithValue("provider_request_id", DbValue(job.ProviderRequestId));
        command.Parameters.AddWithValue("source_body_photo_id", DbValue(job.SourceBodyPhotoId));
        command.Parameters.AddWithValue("retention_until", DbValue(job.RetentionUntil));
        command.Parameters.AddWithValue("is_deleted", job.IsDeleted);
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
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<string[]>(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.GetFieldValue<string[]>(13),
            reader.IsDBNull(14) ? null : reader.GetInt32(14),
            reader.IsDBNull(15) ? null : reader.GetInt32(15),
            reader.GetFieldValue<string[]>(16),
            reader.IsDBNull(17) ? null : reader.GetInt32(17),
            reader.IsDBNull(18) ? null : reader.GetInt32(18),
            reader.IsDBNull(19) ? null : reader.GetInt32(19),
            reader.GetBoolean(20),
            reader.GetBoolean(21),
            reader.IsDBNull(22) ? null : reader.GetFieldValue<DateTimeOffset>(22),
            reader.GetString(23),
            reader.GetFieldValue<DateTimeOffset>(24));
    }

    private static Outfit ReadOutfitShell(NpgsqlDataReader reader)
    {
        return new Outfit(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            Array.Empty<OutfitItem>(),
            reader.GetFieldValue<string[]>(3),
            reader.GetFieldValue<string[]>(4),
            reader.GetBoolean(5),
            reader.GetBoolean(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    private static TryOnJob ReadTryOnJob(NpgsqlDataReader reader)
    {
        return new TryOnJob(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetGuid(2),
            reader.GetString(3),
            reader.GetBoolean(4),
            Enum.Parse<TryOnStatus>(reader.GetString(5)),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.GetFieldValue<DateTimeOffset>(9),
            reader.GetFieldValue<DateTimeOffset>(10))
        {
            ConsentAcceptedAt = reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11),
            ProviderName = reader.IsDBNull(12) ? null : reader.GetString(12),
            ProviderRequestId = reader.IsDBNull(13) ? null : reader.GetString(13),
            SourceBodyPhotoId = reader.IsDBNull(14) ? null : reader.GetGuid(14),
            RetentionUntil = reader.IsDBNull(15) ? null : reader.GetFieldValue<DateTimeOffset>(15),
            IsDeleted = reader.GetBoolean(16)
        };
    }

    private static object DbValue<T>(T? value)
    {
        return value is null ? DBNull.Value : value;
    }
}
