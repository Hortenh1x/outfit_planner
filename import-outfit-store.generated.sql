begin;
insert into users (
    id, email, normalized_email, display_name, password_hash,
    created_at, updated_at, last_login_at, email_verified_at, two_factor_enabled
)
values (
    'usr_11037795d3ff459fa50db6b741720856',
    'dmytro.bolibok@gmail.com',
    'dmytro.bolibok@gmail.com',
    'Dmytro Bolibok',
    NULL,
    '2026-06-24T12:18:49.5749238+00:00',
    '2026-06-24T12:19:52.499235+00:00',
    '2026-06-24T12:19:52.499235+00:00',
    NULL,
    false
)
on conflict (id) do update set
    email = excluded.email,
    normalized_email = excluded.normalized_email,
    display_name = excluded.display_name,
    updated_at = excluded.updated_at,
    last_login_at = excluded.last_login_at;
insert into body_reference_photos (
    id, user_id, image_url, object_key, created_at
)
values (
    '98d7f3ea-2279-4f52-8910-c0d3267e90c5',
    'usr_11037795d3ff459fa50db6b741720856',
    '/api/storage/signed/=1813864535&signature=ioTknaCjfZbTTQnRGg7Tg8QlmXnuk7OCTJJDIPm6JCY',
    'body-reference-photos/original/0fb0dd0ab295454c82822b974129ab6e.jpg',
    '2026-06-24T12:25:14.8813503+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    image_url = excluded.image_url,
    object_key = excluded.object_key,
    created_at = excluded.created_at;
insert into garment_items (
    id, user_id, name, category, body_zone,
    image_url, thumbnail_url,
    object_key, thumbnail_object_key, processed_cutout_object_key,
    tags, primary_color, secondary_colors, material, brand, size,
    season, weather_min_temp, weather_max_temp, occasion,
    formality_score, warmth_score, comfort_score,
    is_favorite, is_archived, last_worn_at, laundry_status, created_at
)
values (
    '4351667f-f390-43f8-849a-7312955f4874',
    'usr_11037795d3ff459fa50db6b741720856',
    '2026 06 24 14 21 08',
    'Top',
    'Torso',
    '/api/storage/signed/=1813864535&signature=-wJP1Ejkc5tI1DuqxTELoF3ovIKkfJrBOZY_yd17kTE',
    '/api/storage/signed/=1813864535&signature=-wJP1Ejkc5tI1DuqxTELoF3ovIKkfJrBOZY_yd17kTE',
    'garments/processed-cutout/6c2da13925324266ac1a4e1b2a2cddb2.jpg',
    'garments/processed-cutout/6c2da13925324266ac1a4e1b2a2cddb2.jpg',
    'garments/processed-cutout/6c2da13925324266ac1a4e1b2a2cddb2.jpg',
    ARRAY['2026','06','24','14','21','08','top']::text[],
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    false,
    false,
    NULL,
    'clean',
    '2026-06-24T12:22:03.2848608+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    category = excluded.category,
    body_zone = excluded.body_zone,
    image_url = excluded.image_url,
    thumbnail_url = excluded.thumbnail_url,
    object_key = excluded.object_key,
    thumbnail_object_key = excluded.thumbnail_object_key,
    processed_cutout_object_key = excluded.processed_cutout_object_key,
    tags = excluded.tags,
    primary_color = excluded.primary_color,
    secondary_colors = excluded.secondary_colors,
    material = excluded.material,
    brand = excluded.brand,
    size = excluded.size,
    season = excluded.season,
    weather_min_temp = excluded.weather_min_temp,
    weather_max_temp = excluded.weather_max_temp,
    occasion = excluded.occasion,
    formality_score = excluded.formality_score,
    warmth_score = excluded.warmth_score,
    comfort_score = excluded.comfort_score,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    last_worn_at = excluded.last_worn_at,
    laundry_status = excluded.laundry_status;
insert into garment_items (
    id, user_id, name, category, body_zone,
    image_url, thumbnail_url,
    object_key, thumbnail_object_key, processed_cutout_object_key,
    tags, primary_color, secondary_colors, material, brand, size,
    season, weather_min_temp, weather_max_temp, occasion,
    formality_score, warmth_score, comfort_score,
    is_favorite, is_archived, last_worn_at, laundry_status, created_at
)
values (
    '45b9a3ff-5fb0-4585-bf87-ac5c5c141ce6',
    'usr_11037795d3ff459fa50db6b741720856',
    '2026 06 24 14 21 25',
    'Top',
    'Torso',
    '/api/storage/signed/=1813864535&signature=igvJeXl17yrXNRQm7B8zN3CxZQ6S6BIGmxcJkxY2smk',
    '/api/storage/signed/=1813864535&signature=igvJeXl17yrXNRQm7B8zN3CxZQ6S6BIGmxcJkxY2smk',
    'garments/processed-cutout/824a478813814964aabbdb640cb522b6.jpg',
    'garments/processed-cutout/824a478813814964aabbdb640cb522b6.jpg',
    'garments/processed-cutout/824a478813814964aabbdb640cb522b6.jpg',
    ARRAY['2026','06','24','14','21','25','top']::text[],
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    false,
    false,
    NULL,
    'clean',
    '2026-06-24T12:22:13.8112776+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    category = excluded.category,
    body_zone = excluded.body_zone,
    image_url = excluded.image_url,
    thumbnail_url = excluded.thumbnail_url,
    object_key = excluded.object_key,
    thumbnail_object_key = excluded.thumbnail_object_key,
    processed_cutout_object_key = excluded.processed_cutout_object_key,
    tags = excluded.tags,
    primary_color = excluded.primary_color,
    secondary_colors = excluded.secondary_colors,
    material = excluded.material,
    brand = excluded.brand,
    size = excluded.size,
    season = excluded.season,
    weather_min_temp = excluded.weather_min_temp,
    weather_max_temp = excluded.weather_max_temp,
    occasion = excluded.occasion,
    formality_score = excluded.formality_score,
    warmth_score = excluded.warmth_score,
    comfort_score = excluded.comfort_score,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    last_worn_at = excluded.last_worn_at,
    laundry_status = excluded.laundry_status;
insert into garment_items (
    id, user_id, name, category, body_zone,
    image_url, thumbnail_url,
    object_key, thumbnail_object_key, processed_cutout_object_key,
    tags, primary_color, secondary_colors, material, brand, size,
    season, weather_min_temp, weather_max_temp, occasion,
    formality_score, warmth_score, comfort_score,
    is_favorite, is_archived, last_worn_at, laundry_status, created_at
)
values (
    '2e751a3e-3295-4551-a00c-f0d238606b29',
    'usr_11037795d3ff459fa50db6b741720856',
    '2026 06 24 14 21 28',
    'Bottom',
    'Legs',
    '/api/storage/signed/=1813864535&signature=s1ibH-PpabiEDrqNALPZ33hgUPCDmtS4E-ecpvPrEH8',
    '/api/storage/signed/=1813864535&signature=s1ibH-PpabiEDrqNALPZ33hgUPCDmtS4E-ecpvPrEH8',
    'garments/processed-cutout/03468e8669ab41c995ceb7d2c60f09d6.jpg',
    'garments/processed-cutout/03468e8669ab41c995ceb7d2c60f09d6.jpg',
    'garments/processed-cutout/03468e8669ab41c995ceb7d2c60f09d6.jpg',
    ARRAY['2026','06','24','14','21','28','top']::text[],
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    false,
    false,
    NULL,
    'clean',
    '2026-06-24T12:22:23.7355772+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    category = excluded.category,
    body_zone = excluded.body_zone,
    image_url = excluded.image_url,
    thumbnail_url = excluded.thumbnail_url,
    object_key = excluded.object_key,
    thumbnail_object_key = excluded.thumbnail_object_key,
    processed_cutout_object_key = excluded.processed_cutout_object_key,
    tags = excluded.tags,
    primary_color = excluded.primary_color,
    secondary_colors = excluded.secondary_colors,
    material = excluded.material,
    brand = excluded.brand,
    size = excluded.size,
    season = excluded.season,
    weather_min_temp = excluded.weather_min_temp,
    weather_max_temp = excluded.weather_max_temp,
    occasion = excluded.occasion,
    formality_score = excluded.formality_score,
    warmth_score = excluded.warmth_score,
    comfort_score = excluded.comfort_score,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    last_worn_at = excluded.last_worn_at,
    laundry_status = excluded.laundry_status;
insert into garment_items (
    id, user_id, name, category, body_zone,
    image_url, thumbnail_url,
    object_key, thumbnail_object_key, processed_cutout_object_key,
    tags, primary_color, secondary_colors, material, brand, size,
    season, weather_min_temp, weather_max_temp, occasion,
    formality_score, warmth_score, comfort_score,
    is_favorite, is_archived, last_worn_at, laundry_status, created_at
)
values (
    '05400d11-88f2-47f0-b6cc-76d64195b627',
    'usr_11037795d3ff459fa50db6b741720856',
    '2026 06 24 14 21 30',
    'Outerwear',
    'OuterLayer',
    '/api/storage/signed/=1813864535&signature=hZtNkyGCfSi2fAdTUNfrLLFFdAA09tHy_ZlXfGOTBUY',
    '/api/storage/signed/=1813864535&signature=hZtNkyGCfSi2fAdTUNfrLLFFdAA09tHy_ZlXfGOTBUY',
    'garments/processed-cutout/d16c9c48399f4bcaba1b91540d7b209b.jpg',
    'garments/processed-cutout/d16c9c48399f4bcaba1b91540d7b209b.jpg',
    'garments/processed-cutout/d16c9c48399f4bcaba1b91540d7b209b.jpg',
    ARRAY['2026','06','24','14','21','30','top']::text[],
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    ARRAY[]::text[],
    NULL,
    NULL,
    NULL,
    false,
    false,
    NULL,
    'clean',
    '2026-06-24T12:22:35.3573118+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    category = excluded.category,
    body_zone = excluded.body_zone,
    image_url = excluded.image_url,
    thumbnail_url = excluded.thumbnail_url,
    object_key = excluded.object_key,
    thumbnail_object_key = excluded.thumbnail_object_key,
    processed_cutout_object_key = excluded.processed_cutout_object_key,
    tags = excluded.tags,
    primary_color = excluded.primary_color,
    secondary_colors = excluded.secondary_colors,
    material = excluded.material,
    brand = excluded.brand,
    size = excluded.size,
    season = excluded.season,
    weather_min_temp = excluded.weather_min_temp,
    weather_max_temp = excluded.weather_max_temp,
    occasion = excluded.occasion,
    formality_score = excluded.formality_score,
    warmth_score = excluded.warmth_score,
    comfort_score = excluded.comfort_score,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    last_worn_at = excluded.last_worn_at,
    laundry_status = excluded.laundry_status;
insert into outfits (
    id, user_id, name, tags, occasion, is_favorite, is_archived,
    clothes_only_preview_url, person_preview_url, created_at
)
values (
    '022dd002-9b02-4be4-8052-a22064b55304',
    'usr_11037795d3ff459fa50db6b741720856',
    'Today',
    ARRAY[]::text[],
    ARRAY[]::text[],
    false,
    false,
    '/generated/clothes-only/4351667f-2e751a3e-05400d11.png',
    NULL,
    '2026-06-24T12:25:22.1420163+00:00'
)
on conflict (id) do update set
    user_id = excluded.user_id,
    name = excluded.name,
    tags = excluded.tags,
    occasion = excluded.occasion,
    is_favorite = excluded.is_favorite,
    is_archived = excluded.is_archived,
    clothes_only_preview_url = excluded.clothes_only_preview_url,
    person_preview_url = excluded.person_preview_url;
insert into outfit_items (outfit_id, garment_id, category)
values (
    '022dd002-9b02-4be4-8052-a22064b55304',
    '4351667f-f390-43f8-849a-7312955f4874',
    'Top'
)
on conflict (outfit_id, garment_id) do update set
    category = excluded.category;
insert into outfit_items (outfit_id, garment_id, category)
values (
    '022dd002-9b02-4be4-8052-a22064b55304',
    '2e751a3e-3295-4551-a00c-f0d238606b29',
    'Bottom'
)
on conflict (outfit_id, garment_id) do update set
    category = excluded.category;
insert into outfit_items (outfit_id, garment_id, category)
values (
    '022dd002-9b02-4be4-8052-a22064b55304',
    '05400d11-88f2-47f0-b6cc-76d64195b627',
    'Outerwear'
)
on conflict (outfit_id, garment_id) do update set
    category = excluded.category;
commit;
