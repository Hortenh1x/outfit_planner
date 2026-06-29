begin;
update body_reference_photos
set image_url = 'https://localhost:5173/api/storage/signed/body-reference-photos/original/0fb0dd0ab295454c82822b974129ab6e.jpg?expires=1782304814&signature=oVbtH_DecFT_JxHoKNma7mMdE-F6syqg0WMhBPjK8BI'
where id = '98d7f3ea-2279-4f52-8910-c0d3267e90c5';
update garment_items
set
    image_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/6c2da13925324266ac1a4e1b2a2cddb2.jpg?expires=1782304623&signature=w_WCbmRqQQBn1sf6LeXfcHXd-aYgZBD6k5oqzP2VgXM',
    thumbnail_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/6c2da13925324266ac1a4e1b2a2cddb2.jpg?expires=1782304623&signature=w_WCbmRqQQBn1sf6LeXfcHXd-aYgZBD6k5oqzP2VgXM'
where id = '4351667f-f390-43f8-849a-7312955f4874';
update garment_items
set
    image_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/824a478813814964aabbdb640cb522b6.jpg?expires=1782304633&signature=9nqTykpEwktSJyZ6097BCmTpadn3kDwf6jnRIHj5Uq8',
    thumbnail_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/824a478813814964aabbdb640cb522b6.jpg?expires=1782304633&signature=9nqTykpEwktSJyZ6097BCmTpadn3kDwf6jnRIHj5Uq8'
where id = '45b9a3ff-5fb0-4585-bf87-ac5c5c141ce6';
update garment_items
set
    image_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/03468e8669ab41c995ceb7d2c60f09d6.jpg?expires=1782304643&signature=ROABjyVf0Uoi5sJoSv-UeFb0wJXnQT8pBfTtDmCfE5M',
    thumbnail_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/03468e8669ab41c995ceb7d2c60f09d6.jpg?expires=1782304643&signature=ROABjyVf0Uoi5sJoSv-UeFb0wJXnQT8pBfTtDmCfE5M'
where id = '2e751a3e-3295-4551-a00c-f0d238606b29';
update garment_items
set
    image_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/d16c9c48399f4bcaba1b91540d7b209b.jpg?expires=1782304655&signature=BOT55yW0zRn_ENeDQZsOTd4k4B825zxtDFwykJsUlgs',
    thumbnail_url = 'https://localhost:5173/api/storage/signed/garments/processed-cutout/d16c9c48399f4bcaba1b91540d7b209b.jpg?expires=1782304655&signature=BOT55yW0zRn_ENeDQZsOTd4k4B825zxtDFwykJsUlgs'
where id = '05400d11-88f2-47f0-b6cc-76d64195b627';
commit;
