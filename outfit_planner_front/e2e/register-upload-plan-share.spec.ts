import { expect, test } from '@playwright/test';

const pixelPng = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAANSURBVBhXY/j///9/AAn7A/0FQ0XKAAAAAElFTkSuQmCC',
  'base64'
);

test('register upload create try-on plan and share smoke', async ({ page }) => {
  const email = `ada-${Date.now()}@example.test`;

  await page.goto('/register');
  await page.getByLabel(/email/i).fill(email);
  await page.getByLabel(/^password$/i).fill('abc12345');
  await page.getByLabel(/repeat password/i).fill('abc12345');
  await page.getByRole('button', { name: /^register$/i }).click();

  await expect(page).toHaveURL(/\/builder/);

  const garmentInput = page.getByLabel(/add a top in wardrobe/i);
  await garmentInput.setInputFiles({
    name: 'linen-shirt.png',
    mimeType: 'image/png',
    buffer: pixelPng
  });
  await expect(page.getByRole('button', { name: /linen shirt/i })).toBeVisible();

  await page.getByRole('button', { name: /linen shirt/i }).click();
  await page.getByLabel(/outfit name/i).fill('Smoke outfit');
  await page.getByRole('button', { name: /save outfit/i }).click();
  await expect(page.getByRole('button', { name: /share/i })).toBeEnabled();

  await page.getByLabel(/add body photo/i).setInputFiles({
    name: 'body.png',
    mimeType: 'image/png',
    buffer: pixelPng
  });
  await expect(page.getByText(/selected/i)).toBeVisible();
  await page.getByRole('button', { name: /generate preview/i }).click();
  await expect(page.getByText(/try-on job/i)).toBeVisible();

  await page.goto('/calendar');
  await page.getByRole('radio', { name: /smoke outfit/i }).click();
  await page.getByRole('button', { name: /plan day/i }).click();
  await expect(page.getByText(/smoke outfit/i)).toBeVisible();

  await page.goto('/builder');
  await page.getByRole('button', { name: /smoke outfit/i }).click();
  await page.getByRole('button', { name: /share/i }).click();
  await expect(page.getByRole('link', { name: /share\//i })).toBeVisible();
});
