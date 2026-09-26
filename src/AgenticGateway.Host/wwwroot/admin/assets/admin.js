document.addEventListener("click", async (event) => {
  const button = event.target.closest("[data-copy], [data-copy-target]");
  if (!button) return;
  const targetId = button.getAttribute("data-copy-target");
  const target = targetId ? document.getElementById(targetId) : null;
  const value = target ? ("value" in target ? target.value : target.textContent) : button.getAttribute("data-copy");
  if (!value) return;
  const original = button.textContent;
  try {
    await navigator.clipboard.writeText(value);
    button.textContent = "Đã sao chép";
  } catch {
    button.textContent = "Không sao chép được";
  }
  window.setTimeout(() => { button.textContent = original; }, 2200);
});
