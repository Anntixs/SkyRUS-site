// Light / dark theme: kept in the "theme" cookie (read by the server too); without it the system setting wins.
(function () {
  const root = document.documentElement;
  const system = () => matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
  document.addEventListener('click', e => {
    if (!e.target.closest('[data-theme-toggle]')) return;
    const next = (root.dataset.theme || system()) === 'dark' ? 'light' : 'dark';
    root.dataset.theme = next;
    document.cookie = 'theme=' + next + '; path=/; max-age=31536000; samesite=lax';
  });
  // Grade buttons in a protocol: live average and pass/fail preview.
  document.addEventListener('change', e => { const form = e.target.closest('[data-grades]'); if (form) average(form); });
  document.addEventListener('DOMContentLoaded', () => document.querySelectorAll('[data-grades]').forEach(average));
  function average(form) {
    const grades = [...form.querySelectorAll('input[type=radio]:checked')].map(i => +i.value).filter(Boolean);
    const total = form.querySelectorAll('[data-item]').length;
    const out = form.querySelector('[data-average]');
    if (!out) return;
    if (!grades.length) { out.textContent = '—'; return; }
    const avg = grades.reduce((a, b) => a + b, 0) / grades.length;
    const fail = grades.some(g => g < 3);
    out.textContent = avg.toFixed(2) + ' · оценено ' + grades.length + ' из ' + total + (fail ? ' · есть оценки ниже 3' : '');
    out.dataset.state = fail ? 'fail' : grades.length === total ? 'pass' : '';
  }
})();
