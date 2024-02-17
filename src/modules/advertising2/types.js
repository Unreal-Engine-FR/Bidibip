
class AdvertisingKinds {
    static FREELANCE = {type: 0, text: "Freelance"};
    static UNPAID = {type: 1, text: "Bénévolat (non rémunéré)"};
    static INTERN_FREE = {type: 2, text: "Stage (non rémunéré)"};
    static INTERN_PAID = {type: 3, text: "Stage (rémunéré)"};
    static WORK_STUDY = {type: 4, text: "Alternance (rémunéré)"};
    static PAID_LIMITED = {type: 5, text: "CDD (rémunéré)"};
    static PAID_UNLIMITED = {type: 6, text: "CDI (rémunéré)"};

    /**
     * @return {(string)[]}
     */
    static allTexts() {
        return [
            this.FREELANCE.text,
            this.UNPAID.text,
            this.INTERN_FREE.text,
            this.INTERN_PAID.text,
            this.WORK_STUDY.text,
            this.PAID_LIMITED.text,
            this.PAID_UNLIMITED.text
        ]
    }
}

module.exports = {AdvertisingKinds}