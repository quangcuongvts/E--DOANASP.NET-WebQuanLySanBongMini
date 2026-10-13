document.addEventListener('DOMContentLoaded', () => {
  const toggle = document.querySelector('[data-menu]');
  const nav = document.querySelector('[data-nav]');
  if (toggle && nav) {
    toggle.addEventListener('click', () => nav.classList.toggle('open'));
  }

  const sideToggle = document.querySelector('[data-sidebar]');
  const sidebar = document.querySelector('.sidebar');
  if (sideToggle && sidebar) {
    sideToggle.addEventListener('click', () => {
      if (window.innerWidth < 860) sidebar.classList.toggle('open');
      else sidebar.classList.toggle('collapsed');
    });
  }

  const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
  document.querySelectorAll('.js-confirm').forEach(form => {
    form.addEventListener('submit', event => {
      if (form.dataset.confirmed === '1') return;
      event.preventDefault();
      Swal.fire({
        title: form.dataset.title || 'Xác nhận',
        text: form.dataset.text || 'Bạn có chắc chắn muốn tiếp tục?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: '#00C853',
        cancelButtonColor: '#90a4ae',
        confirmButtonText: 'Đồng ý',
        cancelButtonText: 'Hủy'
      }).then(result => {
        if (result.isConfirmed) {
          form.dataset.confirmed = '1';
          form.submit();
        }
      });
    });
  });

  document.querySelectorAll('.js-read').forEach(button => {
    button.addEventListener('click', async () => {
      const body = new URLSearchParams();
      body.set('id', button.dataset.id);
      await fetch(button.dataset.url, {
        method: 'POST',
        headers: { 'RequestVerificationToken': token || '' },
        body
      });
      button.closest('.note-item')?.classList.add('read');
    });
  });

  if (window.tempSuccess) {
    Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: window.tempSuccess, showConfirmButton: false, timer: 2600 });
  }
  if (window.tempError) {
    Swal.fire({ icon: 'error', title: 'Chưa thực hiện được', text: window.tempError });
  }

  const revealItems = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.16 });
    revealItems.forEach(item => observer.observe(item));
  } else {
    revealItems.forEach(item => item.classList.add('in'));
  }
});

function formatVnd(value) {
  return new Intl.NumberFormat('vi-VN').format(value || 0) + ' ₫';
}
