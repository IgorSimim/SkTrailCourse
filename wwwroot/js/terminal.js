// ========== ELEMENTOS DOM ==========
const elements = {
    outputText: document.getElementById('outputText'),
    commandInput: document.getElementById('commandInput'),
    terminalOutput: document.getElementById('terminalOutput'),
    normalInput: document.getElementById('normalInput'),
    cpfInput: document.getElementById('cpfInput'),
    customerCpfInput: document.getElementById('customerCpfInput'),
    sendButton: document.getElementById('sendButton'),
    sendText: document.getElementById('sendText'),
    loadingSpinner: document.getElementById('loadingSpinner')
};

// Defensive fixes: create or fallback elements so a missing DOM node doesn't break the flow
if (!elements.outputText) {
    // Try to attach an outputText div inside terminalOutput if possible
    const out = document.createElement('div');
    out.id = 'outputText';
    if (elements.terminalOutput) {
        elements.terminalOutput.appendChild(out);
    } else {
        // last resort: append to body
        document.body.appendChild(out);
    }
    elements.outputText = out;
}

// Ensure cpf input is hidden initially (matches previous behavior)
if (elements.cpfInput) {
    elements.cpfInput.style.display = 'none';
}

// ========== INICIALIZAÇÃO ==========
elements.commandInput.focus();

elements.commandInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') sendCommand();
});

elements.customerCpfInput.addEventListener('keypress', (e) => {
    if (e.key === 'Enter') searchBoletos();
});

// ========== FUNÇÕES PRINCIPAIS ==========
async function sendCommand() {
    const command = elements.commandInput.value.trim();
    if (!command) return;

    toggleLoading(true);
    addToTerminal(`<div class="message-container"><span class="user-message">💬 > ${command}</span></div>`, true);
    elements.commandInput.value = '';

    try {
        const response = await fetch('/Home/ProcessCommand', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ command })
        });

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const result = await response.json();

        if (result.isExit) {
            addToTerminal(`<div class="message-container"><span class="system-message">${result.message}</span></div>`, true);
            elements.commandInput.disabled = true;
            return;
        }

        if (result.requiresCpfInput) {
            elements.normalInput.style.display = 'none';
            elements.cpfInput.style.display = 'block';
            elements.customerCpfInput.focus();
        }

        const formattedMessage = applyMessageColors(result.message);
        addToTerminal(formattedMessage, true);

    } catch (error) {
        addToTerminal(`<div class="message-container"><span class="error-message">❌ Erro de conexão: ${error.message}. Tente novamente.</span></div>`, true);
    } finally {
        toggleLoading(false);
    }
}

async function searchBoletos() {
    const customerCpf = elements.customerCpfInput.value.trim();
    if (!customerCpf) {
        addToTerminal('<div class="message-container"><span class="error-message">❌ Por favor, digite um CPF válido.</span></div>', true);
        return;
    }

    addToTerminal(`<div class="message-container"><span class="info-message">👤 CPF informado: ${customerCpf}</span></div>`, true);
    toggleLoading(true);

    elements.cpfInput.style.display = 'none';
    elements.normalInput.style.display = 'block';
    elements.customerCpfInput.value = '';

    try {
        const response = await fetch('/Home/SearchBoletos', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ customerCpf })
        });

        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const result = await response.json();
        const formattedMessage = applyMessageColors(result.message);
        addToTerminal(formattedMessage, true);

    } catch (error) {
        addToTerminal(`<div class="message-container"><span class="error-message">❌ Erro na consulta: ${error.message}</span></div>`, true);
    } finally {
        toggleLoading(false);
        elements.commandInput.focus();
    }
}

function cancelCpfInput() {
    elements.cpfInput.style.display = 'none';
    elements.normalInput.style.display = 'block';
    elements.customerCpfInput.value = '';
    elements.commandInput.focus();
    addToTerminal('<div class="message-container"><span class="warning-message">❌ Consulta de boletos cancelada.</span></div>', true);
}

function clearTerminal() {
    elements.outputText.innerHTML = '';
    addToTerminal('<div class="message-container"><span class="system-message">🧹 Terminal limpo.<br>----------------------------------------</span></div>', true);
}

// ========== FUNÇÕES AUXILIARES ==========
function toggleLoading(isLoading) {
    if (elements.sendText) elements.sendText.classList.toggle('d-none', isLoading);
    if (elements.loadingSpinner) elements.loadingSpinner.classList.toggle('d-none', !isLoading);
    if (elements.sendButton) elements.sendButton.disabled = isLoading;
}

function applyMessageColors(message) {
    const formattedMessage = message.replace(/\n/g, '<br>');
    let messageClass = 'system-message';

    if (message.includes('❌') || message.includes('Erro') || message.includes('não pode')) {
        messageClass = 'error-message';
    } else if (message.includes('✅') || message.includes('sucesso') || message.includes('Sucesso')) {
        messageClass = 'success-message';
    } else if (message.includes('⚠️') || message.includes('Atenção') || message.includes('atenção')) {
        messageClass = 'warning-message';
    } else if (message.includes('🔍') || message.includes('Consultando') || message.includes('analisando') || message.includes('💡') || message.includes('Dica')) {
        messageClass = 'info-message';
    }

    return `<div class="message-container"><span class="${messageClass}">${formattedMessage}</span></div>`;
}

function addToTerminal(html, scroll = false) {
    const div = document.createElement('div');
    div.innerHTML = html;
    // append to outputText if available, otherwise fallback to terminalOutput or body
    const container = elements.outputText || elements.terminalOutput || document.body;
    try {
        container.appendChild(div);
    } catch (err) {
        // as a last resort, log to console so it doesn't break execution flow
        console.error('Failed to append terminal output element:', err);
        document.body.appendChild(div);
    }

    if (scroll) {
        elements.terminalOutput.scrollTop = elements.terminalOutput.scrollHeight;
    }
}
