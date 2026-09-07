import { PageHeader } from '../shared/ui/PageHeader';

// Public Privacy Policy (/privacy). GDPR-oriented: controller, data, purposes,
// legal bases, recipients, retention, and rights.
export function PrivacyPage() {
  return (
    <section className="legal-view">
      <PageHeader
        eyebrow="Legal"
        title="Privacy Policy"
        text="Version 2026-08-26. What data Outfit Planner processes, why, and what rights you have."
      />

      <article className="legal-section">
        <h2>1. Controller</h2>
        <p>
          Dmytro Bolibok, contact: <a href="mailto:dmytro.bolibok@gmail.com">dmytro.bolibok@gmail.com</a>.
          Requests about your data (access, correction, deletion, export) are handled via this
          address or directly in the app.
        </p>
      </article>

      <article className="legal-section">
        <h2>2. Data we process</h2>
        <ul>
          <li>Account data: email, optional username and avatar, gender setting, hashed password (never the password itself), consent timestamp and version.</li>
          <li>Content you upload: garment photos (plus processed variants such as background-removed cutouts) and body reference photos.</li>
          <li>Generated content: AI try-on previews linked to your account.</li>
          <li>Service data: outfits, calendar plans, share links, AI-credit ledger, and — if you buy something — subscription status from Stripe. Card data never reaches this service.</li>
          <li>Technical logs (IP, timestamps, request metadata) kept short-term for security and abuse prevention.</li>
        </ul>
      </article>

      <article className="legal-section">
        <h2>3. Purposes &amp; legal bases (GDPR Art 6)</h2>
        <ul>
          <li>Providing the service you signed up for, including storage and image processing — contract (Art 6(1)(b)).</li>
          <li>AI try-on generation — performed only on your explicit per-generation confirmation (Art 6(1)(b); the confirmation dialog is the request).</li>
          <li>Payments and subscription management — contract and legal obligations (Art 6(1)(b), (c)).</li>
          <li>Security, rate limiting, abuse prevention — legitimate interest (Art 6(1)(f)).</li>
        </ul>
        <p>Body reference photos are treated as sensitive by design: they are private to your account, served only through short-lived signed URLs, and never used for identification, profiling, or training.</p>
      </article>

      <article className="legal-section">
        <h2>4. Recipients</h2>
        <ul>
          <li>FASHN (fashn.ai) — receives the selected garment images and your body reference photo solely to produce a try-on render you explicitly requested. FASHN may process data outside the EU; renders are transferred back and stored here.</li>
          <li>Stripe — payment processing and subscription state.</li>
          <li>Cloudflare — network delivery of the public site.</li>
          <li>Google/Apple — only if you choose their sign-in.</li>
        </ul>
        <p>No data is sold or used for advertising. No automated decision-making with legal effect takes place.</p>
      </article>

      <article className="legal-section">
        <h2>5. Retention &amp; deletion</h2>
        <p>
          Your data is kept while your account exists. Deleting a photo, an AI output, or the whole
          account removes the stored binaries and database records immediately (account deletion
          also purges generated previews and revokes share links). AI outputs additionally carry a
          retention window after which they are eligible for cleanup. You can export your account
          data from the app at any time.
        </p>
      </article>

      <article className="legal-section">
        <h2>6. Your rights</h2>
        <p>
          Under the GDPR you have the rights of access, rectification, erasure, restriction,
          portability, and objection, plus the right to complain to a supervisory authority. The
          in-app tools (export, delete photo/output, delete account) implement the common cases
          instantly; anything else — write to the contact above.
        </p>
      </article>
    </section>
  );
}
