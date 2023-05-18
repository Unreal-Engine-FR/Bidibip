
function format_string(format, ...args) {
    let index = 0;
    return format.replace(/%[sduifl]/g, match => {
        const arg = args[index++];
        return typeof arg !== 'undefined' ? String(arg) : match;
    });
}

class Logger {
    constructor() {
        let log_stdout = process.stdout;
        return
        console.log = (text, ...args) => {

            const text_formatted = format_string(text, args)

            const now = new Date().toISOString();
            const date = now.substring(2, 10) + ' ' + now.substring(11, 19);
            const string = `[${date}] ${text_formatted}\n`;
            log_stdout.write(string);
        };
    }

    print(author, message) {

    }
}

const LOGGER = new Logger()

function get() {
    return LOGGER
}

module.exports = {get}