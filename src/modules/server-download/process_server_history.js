const fs = require("fs")
const CONFIG = require('../../config')
const logger = require('../../utils/logger')
logger.init_logger()

function string_to_unicode(input) {
    let str = ''
    for (let i = 0; i < input.length; ++i) {
        if ((input.charCodeAt(i) >= 32 && input.charCodeAt(i) <= 127) || input.charAt(i) === '\n') {
            str += input.charAt(i)
        }
    }
    return str
}

function get_files() {

    let discussion = []

    for (const channel of fs.readdirSync(`${CONFIG.get().SAVE_DIR}/message_history`)) {
        console.info(`Start process of channel #${channel}`)
        const channel_conversations = []
        for (const chunk of fs.readdirSync(`${CONFIG.get().SAVE_DIR}/message_history/${channel}`).sort((a, b) => Number(a.split('.')[0]) - Number(b.split('.')[0]))) {
            const data = JSON.parse(fs.readFileSync(`${CONFIG.get().SAVE_DIR}/message_history/${channel}/${chunk}`, "utf-8"))
            for (const [k, v] of Object.entries(data)) {
                channel_conversations.push(string_to_unicode(v.t.replaceAll(/[éèê]/g, 'e').replaceAll('ç', 'c')))
            }
        }
        discussion = discussion.concat(channel_conversations.reverse())
    }

    let string = ''
    for (const text of discussion)
        string += `${text}\n`

    fs.writeFileSync(`${CONFIG.get().SAVE_DIR}/generated_save.txt`, string, "utf-8")

    console.log('Saved', discussion.length, 'messages')
}

get_files()


/*
for (const chunk of fs.readdirSync(`./src/modules/server-download/test_a`)) {

    const file = fs.readFileSync(`./src/modules/server-download/test_a/${chunk}`, "utf-8")
    console.log("loaded", chunk)

    const string = file.match(/(.|[\r\n]){1,1000000}/g)
    fs.appendFileSync(`./src/modules/server-download/test_b/${chunk}`, '', "utf-8")
    let i = 1
    for (const s of string) {
        const fixed = string_to_unicode(s)
        console.log('write chunk', i, 'of', string.length)
        i += 1
        fs.appendFileSync(`./src/modules/server-download/test_b/${chunk}`, fixed, "utf-8")
    }
    console.log("wrote", chunk)
}
 */