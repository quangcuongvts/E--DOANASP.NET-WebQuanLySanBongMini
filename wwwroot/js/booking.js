document.addEventListener('DOMContentLoaded', () => {
  const app = document.getElementById('booking-app');
  if (!app) return;

  const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
  const fieldInput = app.querySelector('#FieldId');
  const dateInput = app.querySelector('#BookingDate');
  const startInput = app.querySelector('#StartTime');
  const endInput = app.querySelector('#EndTime');
  const voucherInput = app.querySelector('#VoucherCode');
  const slotBox = app.querySelector('#slot-box');
  const summary = app.querySelector('#quote-summary');
  const message = app.querySelector('#quote-message');
  let anchor = null;

  const headers = { 'Content-Type': 'application/json', 'RequestVerificationToken': token };

  function selectedServices() {
    return [...app.querySelectorAll('.svc-check:checked')].map(box => ({
      serviceId: Number(box.dataset.id),
      quantity: Number(app.querySelector(`.svc-qty[data-id="${box.dataset.id}"]`)?.value || 1)
    }));
  }

  async function loadSlots() {
    if (!fieldInput.value || !dateInput.value) return;
    const response = await fetch(`/Booking/Slots?fieldId=${fieldInput.value}&date=${dateInput.value}`);
    const data = await response.json();
    slotBox.innerHTML = '';
    anchor = null;
    data.slots.forEach(slot => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = `slot slot-${slot.status}`;
      button.dataset.start = slot.start;
      button.dataset.end = slot.end;
      button.dataset.status = slot.status;
      button.innerHTML = `<div>${slot.start} – ${slot.end}</div><small>${slot.statusText} · ${formatVnd(slot.price)}</small>`;
      button.addEventListener('click', () => pick(button));
      slotBox.appendChild(button);
    });
    const presetStart = app.dataset.start;
    const presetEnd = app.dataset.end;
    if (presetStart && presetEnd) {
      const buttons = [...slotBox.querySelectorAll('.slot')];
      const first = buttons.find(b => b.dataset.start === presetStart);
      const last = buttons.find(b => b.dataset.end === presetEnd);
      if (first && last) selectRange(first, last);
    }
  }

  function pick(button) {
    if (button.dataset.status !== 'available' && button.dataset.status !== 'selected') return;
    const buttons = [...slotBox.querySelectorAll('.slot')];
    if (!anchor) {
      anchor = button;
      paint(buttons, button, button);
      return;
    }
    const from = buttons.indexOf(anchor);
    const to = buttons.indexOf(button);
    if (to < from) {
      anchor = button;
      paint(buttons, button, button);
      return;
    }
    selectRange(anchor, button);
  }

  function selectRange(first, last) {
    const buttons = [...slotBox.querySelectorAll('.slot')];
    const from = buttons.indexOf(first);
    const to = buttons.indexOf(last);
    for (let i = from; i <= to; i++) {
      if (buttons[i].dataset.status === 'booked' || buttons[i].dataset.status === 'pending' || buttons[i].dataset.status === 'maintenance') {
        message.textContent = 'Khoảng giờ đang chọn có khung đã bận.';
        anchor = null;
        return;
      }
    }
    paint(buttons, first, last);
    startInput.value = first.dataset.start;
    endInput.value = last.dataset.end;
    anchor = null;
    quote();
  }

  function paint(buttons, first, last) {
    const from = buttons.indexOf(first);
    const to = buttons.indexOf(last);
    buttons.forEach((button, index) => {
      button.classList.remove('slot-selected');
      if (index >= from && index <= to && button.dataset.status === 'available') button.classList.add('slot-selected');
    });
  }

  async function quote() {
    if (!fieldInput.value || !startInput.value || !endInput.value) return;
    const response = await fetch('/Booking/Quote', {
      method: 'POST',
      headers,
      body: JSON.stringify({
        fieldId: Number(fieldInput.value),
        date: dateInput.value,
        start: startInput.value,
        end: endInput.value,
        voucherCode: voucherInput.value,
        services: selectedServices()
      })
    });
    const data = await response.json();
    message.textContent = data.message || (data.ok ? '' : 'Không tính được giá.');
    if (!data.ok) {
      summary.innerHTML = '';
      return;
    }
    const services = (data.serviceLines || []).map(line => `<div class="line"><span>${line.name} x${line.quantity}</span><b>${formatVnd(line.total)}</b></div>`).join('');
    summary.innerHTML = `
      <div class="line"><span>Số giờ</span><b>${data.hours}</b></div>
      <div class="line"><span>Tiền sân</span><b>${formatVnd(data.fieldAmount)}</b></div>
      <div class="line"><span>Giá gốc</span><b>${formatVnd(data.basePrice)}</b></div>
      <div class="line"><span>Phụ thu cao điểm</span><b>${formatVnd(data.peakHourCharge)}</b></div>
      <div class="line"><span>Phụ thu cuối tuần</span><b>${formatVnd(data.weekendCharge)}</b></div>
      ${services}
      <div class="line"><span>Dịch vụ</span><b>${formatVnd(data.serviceAmount)}</b></div>
      <div class="line"><span>Giảm giá</span><b>- ${formatVnd(data.discount)}</b></div>
      <div class="line total-line"><span>Tổng thanh toán</span><b>${formatVnd(data.total)}</b></div>`;
  }

  app.querySelectorAll('.field-choice').forEach(choice => {
    choice.addEventListener('click', () => {
      app.querySelectorAll('.field-choice').forEach(item => item.classList.remove('active'));
      choice.classList.add('active');
      fieldInput.value = choice.dataset.id;
      loadSlots();
    });
  });

  dateInput?.addEventListener('change', () => {
    startInput.value = '';
    endInput.value = '';
    loadSlots();
  });
  app.querySelectorAll('.svc-check, .svc-qty').forEach(el => el.addEventListener('change', quote));
  app.querySelector('#apply-voucher')?.addEventListener('click', async () => {
    const code = app.querySelector('#voucher-text').value.trim();
    voucherInput.value = code;
    await quote();
  });

  const form = app.querySelector('#booking-form');
  form?.addEventListener('submit', event => {
    if (!fieldInput.value || !startInput.value || !endInput.value) {
      event.preventDefault();
      Swal.fire('Thiếu thông tin', 'Hãy chọn sân, ngày và khung giờ.', 'warning');
      return;
    }
    form.querySelectorAll('.svc-hidden').forEach(node => node.remove());
    selectedServices().forEach((service, index) => {
      ['ServiceId', 'Quantity'].forEach(key => {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.className = 'svc-hidden';
        input.name = `Services[${index}].${key}`;
        input.value = key === 'ServiceId' ? service.serviceId : service.quantity;
        form.appendChild(input);
      });
    });
  });

  document.querySelector('#lookup-customer')?.addEventListener('click', async () => {
    const phone = document.querySelector('#WalkInPhone')?.value;
    const response = await fetch(`/Admin/Bookings/LookupCustomer?phone=${encodeURIComponent(phone || '')}`);
    const data = await response.json();
    const box = document.querySelector('#lookup-result');
    if (data.found) {
      document.querySelector('#CustomerId').value = data.id;
      box.textContent = `Đã tìm thấy ${data.fullName} - ${data.phone}`;
    } else {
      document.querySelector('#CustomerId').value = '';
      box.textContent = 'Chưa có khách này. Hệ thống sẽ tạo khách vãng lai khi lưu.';
    }
  });

  if (fieldInput.value) loadSlots();
});
