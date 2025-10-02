document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('chat-form');
    const input = document.getElementById('message-input');
    const chatBox = document.getElementById('chat-box');

    function formatText(text) {
        if (typeof text === 'object') {
            try {
                return JSON.stringify(text, null, 2);
            } catch {
                return '[objeto]';
            }
        }
        // Links e quebras de linha
        return String(text)
            .replace(/(https?:\/\/\S+)/g, '<a href="$1" target="_blank">$1</a>')
            .replace(/\n/g, '<br>');
    }

    function appendMessage(text, sender) {
        const wrapper = document.createElement('div');
        wrapper.className = sender === 'user' ? 'd-flex flex-column align-items-end' : 'd-flex flex-column align-items-start';
        const meta = document.createElement('div');
        meta.className = 'chat-meta';
        meta.innerText = sender === 'user' ? 'Você' : 'Assistente';
        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${sender}`;
        bubble.innerHTML = formatText(text);
        wrapper.appendChild(meta);
        wrapper.appendChild(bubble);
        chatBox.appendChild(wrapper);
        chatBox.scrollTop = chatBox.scrollHeight;
    }

    form.addEventListener('submit', function (e) {
        e.preventDefault();
        const message = input.value.trim();
        if (!message) return;
        appendMessage(message, 'user');
        input.value = '';
        fetch('/Home/SendMessage', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: `message=${encodeURIComponent(message)}`
        })
        .then(res => res.json())
        .then(data => {
            let resp = data.response;
            if (typeof resp === 'object') {
                resp = JSON.stringify(resp, null, 2);
            }
            appendMessage(resp, 'bot');
        })
        .catch(() => {
            appendMessage('Erro ao enviar mensagem.', 'bot');
        });
    });
});
