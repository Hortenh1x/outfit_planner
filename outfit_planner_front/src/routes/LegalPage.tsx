import { Link } from 'react-router-dom';
import { PageHeader } from '../shared/ui/PageHeader';

// Public legal & service information page (/legal). Reachable without an account:
// linked from the account settings dialog, but shoppers must be able to read
// payment/refund terms before signing up.
export function LegalPage() {
  return (
    <section className="legal-view">
      <PageHeader
        eyebrow="Information"
        title="Legal & service information"
        text="Who runs Outfit Planner, what the demo does with your data, and how payments, subscriptions, and AI credits work."
      />

      <article className="legal-section">
        <h2>Documents</h2>
        <p>
          The binding documents accepted at sign-up: <Link to="/terms">Terms of Use</Link> and{' '}
          <Link to="/privacy">Privacy Policy</Link>. The sections below summarise how the service
          operates.
        </p>
      </article>

      <article className="legal-section">
        <h2>Service provider &amp; contact</h2>
        <p>
          Outfit Planner is a personal demo project operated by Dmytro Bolibok.
          Questions, feedback, data requests, or AI-feature demo requests:{' '}
          <a href="mailto:dmytro.bolibok@gmail.com">dmytro.bolibok@gmail.com</a>.
        </p>
      </article>

      <article className="legal-section">
        <h2>Demo service</h2>
        <p>
          This application is a demonstration, provided as-is without uptime or support guarantees.
          AI try-on generation depends on paid third-party compute and is enabled on request only;
          the composed-figure preview, wardrobe cataloging, planning, and sharing always work.
        </p>
      </article>

      <article className="legal-section">
        <h2>Accounts &amp; your data</h2>
        <p>
          An account stores your email, an optional username, avatar, and gender setting, plus the
          content you upload: garment photos and body reference photos. Body reference photos are
          private and only ever served through short-lived signed URLs. You can export everything
          your account holds and delete the account (including all stored photos and generated
          images) at any time from the app; deletion is immediate and irreversible.
        </p>
      </article>

      <article className="legal-section">
        <h2>AI processing</h2>
        <p>
          Garment background removal runs locally on our server. When you explicitly confirm an AI
          try-on generation, the selected garment images and your body reference photo are sent to
          the FASHN API (fashn.ai) to produce the render; results are stored back in this app under
          your account and can be deleted by you. Nothing is sent to AI providers without your
          explicit confirmation.
        </p>
      </article>

      <article className="legal-section">
        <h2>Payments, subscriptions &amp; credits</h2>
        <p>
          Payments are processed by Stripe; this application never sees or stores card numbers. The
          Premium subscription renews monthly and can be cancelled any time from the billing portal
          (account settings → Manage subscription) — cancellation stops future charges and Premium
          lasts until the end of the paid period. AI credits (the monthly Premium allowance and
          one-off top-up packs) never expire, are tied to your account, and are consumed per
          generation. Credits are refunded automatically when a generation fails; as a digital
          service consumed on demand, purchased credits are otherwise non-refundable except where
          consumer law requires it. Prices shown on the upgrade page are the prices charged at
          checkout.
        </p>
      </article>

      <article className="legal-section">
        <h2>Third-party services</h2>
        <ul>
          <li>Stripe — payment processing and subscription management.</li>
          <li>FASHN (fashn.ai) — AI try-on rendering, only on your explicit request.</li>
          <li>Cloudflare — network delivery of the public site.</li>
          <li>Google sign-in — optional authentication, only if you choose it.</li>
        </ul>
      </article>

      <article className="legal-section">
        <h2>Content responsibility</h2>
        <p>
          Upload only photos you have the right to use. Shared outfit links are public to anyone
          who has the link and can be revoked by you at any time.
        </p>
      </article>
    </section>
  );
}
