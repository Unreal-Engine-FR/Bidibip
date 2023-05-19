class Configuration {
    constructor() {

        this.APP_TOKEN = null
        this.APP_ID = null
        this.SERVER_ID = null
        this.LOG_CHANNEL_ID = null

        this.ADVERTISING_UNPAID_CHANNEL = null
        this.ADVERTISING_PAID_CHANNEL = null
        this.ADVERTISING_FREELANCE_CHANNEL = null
        this.SHARED_SHARED_CHANNEL = null

        this.MEMBER_PERMISSION_FLAG=16384 // Send link
        this.ADMIN_PERMISSION_FLAG=4 // Ban member

        this.CACHE_DIR='../Bidibip_Cache/'
        this.SAVE_DIR='./saved/'
        this.LOG_KEEP_DAYS=14
        this.SERVICE_ROLE='<@285426910404673536>'
        this.UPDATE_FOLLOW_BRANCH='dev'

        require('dotenv').config();
        for (const [key, value] of Object.entries(process.env)) {
            this[key] = value
        }
    }
}

let CONFIG = null

function get() {
    if (CONFIG === null)
        CONFIG = new Configuration()
    return CONFIG
}

module.exports = {get}