"use strict";

const form = document.getElementById("import-form");
const button = document.getElementById("import-button");
const status = document.getElementById("import-status");

function cell(row, text, tag = "td") {
    const element = document.createElement(tag);
    element.textContent = text;
    row.append(element);
    return element;
}

function table(containerId, headers) {
    const container = document.getElementById(containerId);
    container.replaceChildren();
    const wrapper = document.createElement("div");
    wrapper.className = "table-wrap";
    const element = document.createElement("table");
    const head = element.createTHead().insertRow();
    headers.forEach(header => cell(head, header, "th"));
    const body = element.createTBody();
    wrapper.append(element);
    container.append(wrapper);
    return body;
}

form.addEventListener("submit", async event => {
    event.preventDefault();
    if (button.disabled) return;
    const file = document.getElementById("csv-file").files[0];
    if (!file) return;
    document.getElementById("import-result").hidden = true;
    status.className = "";
    if (file.size > 5 * 1024 * 1024) {
        status.textContent = "CSV dosyası en fazla 5 MiB olabilir.";
        status.className = "error";
        return;
    }
    button.disabled = true;
    status.textContent = "Dosya işleniyor…";
    try {
        const response = await fetch(form.action, { method: "POST", body: new FormData(form) });
        const report = await response.json();
        if (!response.ok) throw new Error(report.dosyaHatasi || report.title || "Dosya yüklenemedi.");

        document.getElementById("result-summary").textContent = `${report.toplam} kayıt işlendi: ${report.kaydedilen} kaydedildi, ${report.degismeyen} değişmedi, ${report.reddedilen} reddedildi.`;
        const errors = table("result-errors", ["CSV satırı", "Alan", "Hata nedeni"]);
        report.hatalar.forEach(error => {
            const row = errors.insertRow();
            cell(row, error.satir); cell(row, error.alan); cell(row, error.mesaj);
        });
        errors.closest("div").hidden = report.hatalar.length === 0;

        const products = table("result-products", ["CSV satırı", "GTIN", "Durum", "Çıktılar"]);
        report.urunler.forEach(product => {
            const row = products.insertRow();
            cell(row, product.satir); cell(row, product.gtin);
            cell(row, product.durum === "kaydedildi" ? "Kaydedildi" : "Değişmedi");
            const links = cell(row, "");
            links.className = "links";
            // Aynı sunucudaki göreli yollar yerel kullanımda da erişilebilir kalır.
            [["HTML", `/dpp/${product.gtin}`], ["JSON-LD", `/dpp/${product.gtin}.jsonld`], ["QR PNG", `/dpp/${product.gtin}/qr.png`]].forEach(([label, path]) => {
                const link = document.createElement("a");
                link.textContent = label; link.href = path;
                if (label === "QR PNG") link.download = `${product.gtin}.png`;
                links.append(link);
            });
        });
        products.closest("div").hidden = report.urunler.length === 0;
        document.getElementById("import-result").hidden = false;
        status.textContent = report.tamamlandi ? "İçe aktarma tamamlandı." : "CSV okuması durduruldu. Önceki geçerli kayıtlar korundu; hata tablosunu inceleyin.";
        if (!report.tamamlandi) status.className = "error";
    } catch (error) {
        status.textContent = error instanceof SyntaxError ? "Sunucu yanıtı okunamadı. Ürün listesini kontrol edin; bazı kayıtlar işlenmiş olabilir." : error.message;
        status.className = "error";
    } finally {
        button.disabled = false;
    }
});
