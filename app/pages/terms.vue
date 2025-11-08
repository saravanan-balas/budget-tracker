<template>
  <div class="max-w-4xl mx-auto py-16 px-4 sm:px-6 lg:px-8">
    <div class="bg-blue-50 border border-blue-200 text-blue-800 rounded-md p-4 mb-8">
      <h2 class="text-lg font-semibold mb-2">Notice</h2>
      <p>
        This Terms of Service content is provided as a placeholder template. Replace it with your
        organization&apos;s official policy before going live.
      </p>
    </div>

    <article class="bg-white rounded-lg shadow-sm border border-gray-200 p-8 space-y-8">
      <header>
        <h1 class="text-3xl font-bold text-gray-900 mb-4">Terms of Service</h1>
        <p class="text-gray-600">Effective Date: {{ effectiveDate }}</p>
      </header>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">1. Acceptance of Terms</h2>
        <p class="text-gray-700 leading-relaxed">
          By accessing or using Budget Tracker, you agree to be bound by these Terms. If you do not agree,
          do not use the service.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">2. Use of the Service</h2>
        <ul class="list-disc list-inside space-y-2 text-gray-700 leading-relaxed">
          <li>You must be at least 18 years old or have parental consent.</li>
          <li>You are responsible for maintaining the confidentiality of your account.</li>
          <li>You agree not to misuse the service or attempt to compromise its security.</li>
        </ul>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">3. Financial Data</h2>
        <p class="text-gray-700 leading-relaxed">
          You retain ownership of the financial data you upload. By using the service, you grant us a limited
          license to process that data solely for providing and improving the service.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">4. Termination</h2>
        <p class="text-gray-700 leading-relaxed">
          We reserve the right to suspend or terminate access if we believe you have violated these Terms or
          engaged in fraudulent activity.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">5. Changes to These Terms</h2>
        <p class="text-gray-700 leading-relaxed">
          We may update these Terms from time to time. If material changes occur, we will notify you through
          the service or via email. Continued use after changes means you accept the revised Terms.
        </p>
      </section>

      <section>
        <h2 class="text-xl font-semibold text-gray-900 mb-3">6. Contact</h2>
        <p class="text-gray-700 leading-relaxed">
          If you have questions regarding these Terms, contact us at
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

