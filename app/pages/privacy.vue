<template>
  <div class="max-w-4xl mx-auto py-16 px-4 sm:px-6 lg:px-8">
    <div class="bg-blue-50 border border-blue-200 text-blue-800 rounded-md p-4 mb-8">
      <h2 class="text-lg font-semibold mb-2">Notice</h2>
      <p>
        This Privacy Policy content is provided as a placeholder template. Replace it with your
        organization&apos;s official policy before going live.
      </p>
    </div>

    <article class="bg-white rounded-lg shadow-sm border border-gray-200 p-8 space-y-8">
      <header>
        <h1 class="text-3xl font-bold text-gray-900 mb-4">Privacy Policy</h1>
        <p class="text-gray-600">Effective Date: {{ effectiveDate }}</p>
      </header>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">1. Information We Collect</h2>
        <p class="text-gray-700 leading-relaxed">
          We collect information you provide when using Budget Tracker, including account details,
          uploaded financial files, and interactions with the service.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">2. How We Use Information</h2>
        <ul class="list-disc list-inside space-y-2 text-gray-700 leading-relaxed">
          <li>To deliver and maintain the Budget Tracker service.</li>
          <li>To improve features and diagnose technical issues.</li>
          <li>To communicate with you about updates or support.</li>
        </ul>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">3. Data Sharing</h2>
        <p class="text-gray-700 leading-relaxed">
          We do not sell your personal data. We may share limited information with service providers who
          help us operate the platform, subject to confidentiality obligations.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">4. Data Security</h2>
        <p class="text-gray-700 leading-relaxed">
          We implement administrative, technical, and physical safeguards designed to protect your data.
          However, no online system can be guaranteed 100% secure.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">5. Your Choices</h2>
        <p class="text-gray-700 leading-relaxed">
          You may request access, updates, or deletion of your account by contacting us. Deleting your
          account may remove uploaded financial data, subject to retention requirements.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">6. Policy Updates</h2>
        <p class="text-gray-700 leading-relaxed">
          We may update this Privacy Policy periodically. We will notify you of significant changes through
          the service or via email. Continued use signifies acceptance of the updated policy.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">7. Contact</h2>
        <p class="text-gray-700 leading-relaxed">
          For questions regarding this policy, contact us at
          <a class="text-blue-600 hover:underline" href="mailto:saravanan.b@hotmail.com">saravanan.b@hotmail.com</a>.
        </p>
      </section>
    </article>
  </div>
</template>

<script setup lang="ts">
const effectiveDate = new Date().toLocaleDateString('en-US', {
  month: 'long',
  day: 'numeric',
  year: 'numeric'
})
</script>

