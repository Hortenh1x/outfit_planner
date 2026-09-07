import { Link } from 'react-router-dom';
import { PageHeader } from '../shared/ui/PageHeader';

// Public Terms of Use (/terms). Accepted at registration (checkbox) and by the
// sign-in notice; the backend stamps the accepted version per account.
export function TermsPage() {
  return (
    <section className="legal-view">
      <PageHeader
        eyebrow="Legal"
        title="Terms of Use"
        text="Version 2026-08-26. These terms govern your use of the Outfit Planner demo service."
      />

      <article className="legal-section">
        <h2>1. The service</h2>
        <p>
          Outfit Planner is a personal demo application operated by Dmytro Bolibok (contact:{' '}
          <a href="mailto:dmytro.bolibok@gmail.com">dmytro.bolibok@gmail.com</a>). It lets you
          catalogue garments, compose outfits, plan a calendar, share looks, and — when enabled —
          generate AI try-on previews. The service is provided as-is, without warranties of
          availability, fitness for a particular purpose, or support. Features may change or be
          withdrawn at any time; AI generation depends on third-party compute and is enabled on
          request only.
        </p>
      </article>

      <article className="legal-section">
        <h2>2. Your account</h2>
        <p>
          You must provide a working email address (or use Google/Apple sign-in) and keep your
          credentials confidential. You are responsible for activity under your account. You can
          delete your account at any time from the app; deletion permanently removes your data as
          described in the <Link to="/privacy">Privacy Policy</Link>.
        </p>
      </article>

      <article className="legal-section">
        <h2>3. Your content</h2>
        <p>
          You keep all rights to the photos you upload. Upload only images you have the right to
          use, and only photos of yourself or of people who have consented. Do not upload unlawful
          content or images of minors. You grant the service the technical licence needed to store,
          process (background removal, resizing, AI try-on on your explicit request), and display
          your content back to you. Shared outfit links are visible to anyone with the link and can
          be revoked by you.
        </p>
      </article>

      <article className="legal-section">
        <h2>4. AI-generated content</h2>
        <p>
          Try-on previews are synthetic images produced by an AI system (FASHN) from your body
          reference photo and garment photos, only after your explicit confirmation. Generated
          previews are labelled "AI-generated" in the app and on shared pages. They are
          visualisations, not faithful depictions of how clothing fits; do not present them as
          authentic photographs. Generating illegal, deceptive, or harmful imagery is prohibited.
        </p>
      </article>

      <article className="legal-section">
        <h2>5. Plans, credits &amp; payments</h2>
        <p>
          Payments are processed by Stripe. The Premium subscription renews monthly and can be
          cancelled any time from the billing portal; cancellation stops future charges and Premium
          remains active until the end of the paid period. AI credits (trial, monthly Premium
          allowance, and top-up packs) are account-bound, never expire, and are consumed per
          generation; credits are automatically refunded when a generation fails. As a digital
          service delivered immediately, credit purchases are otherwise non-refundable except where
          consumer law provides mandatory rights. Prices shown on the upgrade page are the prices
          charged at checkout.
        </p>
      </article>

      <article className="legal-section">
        <h2>6. Acceptable use</h2>
        <p>
          No abuse: do not attempt to break, overload, or reverse the service, scrape other users'
          data, evade rate limits or credit metering, or use the service to infringe the rights of
          others. Accounts violating these terms can be suspended or removed.
        </p>
      </article>

      <article className="legal-section">
        <h2>7. Liability &amp; changes</h2>
        <p>
          To the extent permitted by law, liability is limited to the amount you paid for the
          service in the preceding twelve months; mandatory statutory liability (including for
          intent and gross negligence) remains unaffected. These terms may be updated; material
          changes are announced by bumping the version shown above, and continued use after a
          change constitutes acceptance. If a provision is invalid, the rest remains in force.
        </p>
      </article>
    </section>
  );
}
