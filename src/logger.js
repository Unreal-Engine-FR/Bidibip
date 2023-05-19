const fs = require('fs')
const CONFIG = require('./config').get()

function arg_to_string(arg, depth = 0) {
    if (Array.isArray(arg)) {
        let res = ''
        for (const item of arg)
            res += `${arg_to_string(item, depth + 1)} `
        return res.substring(0, res.length - 1)
    } else if (arg === Object(arg))
        return arg.constructor.name + ' ' + JSON.stringify(arg, null, 4)

    return String(arg)
}

function format_string(format, ...args) {
    const text_format = arg_to_string(format)
    let index = 0
    let text = text_format.replace(/%s/g, match => {
        const arg = arg_to_string(args[index++])
        return typeof arg !== 'undefined' ? arg : match
    })
    while (index < args.length) {
        text += ` ${arg_to_string(args[index++])}`
    }
    return text
}

class Logger {
    constructor() {
        console.log = (message, ...args) => {
            this._internal_print('D', format_string(message, args))
        }

        console.info = this.info
        console.validate = this.validate
        console.warning = this.warning
        console.error = this.error
        console.fatal = this.fatal


        const log_dir = __dirname + '/../' + CONFIG.LOG_DIR
        let date_str = new Date().toLocaleString().replaceAll('/', '-').replaceAll(':', '.').replaceAll(', ', '_')
        this.log_file = `${log_dir}/${date_str}.log`

        if (!fs.existsSync(log_dir))
            fs.mkdirSync(log_dir, {recursive: true})

        fs.readdir(log_dir, (err, files) => {
            files.forEach(file => {
                const file_full = log_dir + '/' + file
                const days = (new Date() - fs.statSync(file_full).mtime) / 1000 / 60 / 60 / 24
                if (days >= CONFIG.LOG_KEEP_DAYS) {
                    this.info(`removed old log file : ${file_full}`)
                    fs.rmSync(file_full)
                }
            })
        })

        this._delegates = []
    }

    bind(func) {
        this._delegates.push(func)
    }

    info(message, ...args) {
        LOGGER._internal_print('I', format_string(message, args))
    }

    validate(message, ...args) {
        LOGGER._internal_print('V', format_string(message, args))
    }

    warning(message, ...args) {
        LOGGER._internal_print('W', format_string(message, args))
    }

    error(message, ...args) {
        LOGGER._internal_print('E', format_string(message, args) + '\n' + Error().stack)
    }

    fatal(message, ...args) {
        LOGGER._internal_print('F', format_string(message, args))
    }

    _internal_print(level, message) {
        let output_message = `[${new Date().toLocaleString()}] [${level}] ${message}\n`


        fs.appendFileSync(this.log_file, output_message);

        process.stdout.write(output_message)

        for (const delegate of this._delegates)
            delegate(level, message)

        if (level === 'F') {
            fs.appendFileSync(this.log_file, Error().stack + '\n');
            throw new Error(message + '\n' + Error().stack)
        }
    }
}

const LOGGER = new Logger()

function get() {
    return LOGGER
}

module.exports = {get}